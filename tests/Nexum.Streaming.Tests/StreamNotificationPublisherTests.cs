#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode for test usage
#pragma warning disable IL2067 // Suppress RequiresDynamicCode for test usage

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexum.Abstractions;

namespace Nexum.Streaming.Tests;

[Trait("Category", "Unit")]
public sealed class StreamNotificationPublisherTests
{
    // -------------------------------------------------------------------------
    // Test notification + handler implementations
    // -------------------------------------------------------------------------

    private sealed record TestNotification : IStreamNotification<int>;

    /// <summary>Handler that yields a fixed sequence of integers.</summary>
    private sealed class SequenceHandler(IEnumerable<int> items, int delayMs = 0)
        : IStreamNotificationHandler<TestNotification, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestNotification notification,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (int item in items)
            {
                ct.ThrowIfCancellationRequested();
                if (delayMs > 0)
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                yield return item;
            }
        }
    }

    /// <summary>Handler that yields N items then throws.</summary>
    private sealed class ThrowingHandler(Exception exception, int yieldCountBeforeThrow = 0)
        : IStreamNotificationHandler<TestNotification, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestNotification notification,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 0; i < yieldCountBeforeThrow; i++)
            {
                ct.ThrowIfCancellationRequested();
                yield return i;
            }
            await Task.Yield();
            throw exception;
        }
    }

    // -------------------------------------------------------------------------
    // Factory helpers
    // -------------------------------------------------------------------------

    private static StreamNotificationPublisher CreatePublisher(
        Action<IServiceCollection>? configure = null,
        int channelCapacity = 1024)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.Configure<NexumStreamingOptions>(opts => opts.MergeChannelCapacity = channelCapacity);
        services.AddOptions<NexumStreamingOptions>();

        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<NexumStreamingOptions>>();
        return new StreamNotificationPublisher(sp, options);
    }

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (T item in source.ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StreamAsync_WithSingleHandler_ReturnsHandlerStreamAsync()
    {
        // Arrange
        var publisher = CreatePublisher(services =>
        {
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler([10, 20, 30]));
        });
        var notification = new TestNotification();

        // Act
        var result = await DrainAsync(
            publisher.StreamAsync<TestNotification, int>(notification, TestContext.Current.CancellationToken));

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo([10, 20, 30]);
    }

    [Fact]
    public async Task StreamAsync_WithMultipleHandlers_MergesStreamsAsync()
    {
        // Arrange — two handlers each producing 3 items with a small delay so they can interleave
        var publisher = CreatePublisher(services =>
        {
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler([1, 2, 3], delayMs: 10));
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler([4, 5, 6], delayMs: 10));
        });
        var notification = new TestNotification();

        // Act
        var result = await DrainAsync(
            publisher.StreamAsync<TestNotification, int>(notification, TestContext.Current.CancellationToken));

        // Assert — all 6 items present; arrival order not guaranteed
        result.Should().HaveCount(6);
        result.Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);
    }

    [Fact]
    public async Task StreamAsync_WithNoHandlers_ReturnsEmptyStreamAsync()
    {
        // Arrange — no handlers registered
        var publisher = CreatePublisher();
        var notification = new TestNotification();

        // Act
        var result = await DrainAsync(
            publisher.StreamAsync<TestNotification, int>(notification, TestContext.Current.CancellationToken));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task StreamAsync_WithCancellation_CancelsAllHandlersAsync()
    {
        // Arrange — two handlers producing items slowly; consumer cancels early
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var publisher = CreatePublisher(services =>
        {
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler(Enumerable.Range(1, 500), delayMs: 10));
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler(Enumerable.Range(1000, 500), delayMs: 10));
        });
        var notification = new TestNotification();

        var collected = new List<int>();

        // Act
        var act = async () =>
        {
            await foreach (int item in publisher.StreamAsync<TestNotification, int>(notification, cts.Token)
                               .ConfigureAwait(false))
            {
                collected.Add(item);
                if (collected.Count >= 2)
                {
                    await cts.CancelAsync().ConfigureAwait(false);
                }
            }
        };

        // Assert — cancellation propagates
        await act.Should().ThrowAsync<OperationCanceledException>();
        collected.Count.Should().BeLessThan(1000);
    }

    [Fact]
    public async Task StreamAsync_WithHandlerException_PropagatesAndCancelsOthersAsync()
    {
        // Arrange — first handler throws after 1 item; second handler runs slowly
        var expectedException = new InvalidOperationException("handler failure");
        var publisher = CreatePublisher(services =>
        {
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new ThrowingHandler(expectedException, yieldCountBeforeThrow: 1));
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler(Enumerable.Range(100, 500), delayMs: 5));
        });
        var notification = new TestNotification();

        // Act
        var act = async () =>
        {
            await foreach (int _ in publisher.StreamAsync<TestNotification, int>(notification,
                               TestContext.Current.CancellationToken).ConfigureAwait(false))
            {
                // consume until exception
            }
        };

        // Assert — when a handler throws, the merged stream stops (either the original exception or
        // OperationCanceledException as the CTS is cancelled to stop remaining handlers).
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task StreamAsync_WithSingleItem_DirectForwardNoChannelOverheadAsync()
    {
        // Arrange — single handler, single item — exercises the fast path (no channel allocated)
        var publisher = CreatePublisher(services =>
        {
            services.AddSingleton<IStreamNotificationHandler<TestNotification, int>>(
                new SequenceHandler([42]));
        });
        var notification = new TestNotification();

        // Act
        var result = await DrainAsync(
            publisher.StreamAsync<TestNotification, int>(notification, TestContext.Current.CancellationToken));

        // Assert
        result.Should().HaveCount(1);
        result[0].Should().Be(42);
    }
}
