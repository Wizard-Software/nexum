#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode for test usage
#pragma warning disable IL2067 // Suppress RequiresDynamicCode for test usage

using System.Runtime.CompilerServices;
using Nexum.Streaming.Internal;

namespace Nexum.Streaming.Tests;

[Trait("Category", "Unit")]
public sealed class StreamMergerTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async IAsyncEnumerable<T> ToAsyncEnumerableAsync<T>(
        IEnumerable<T> items,
        int delayMs = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (T item in items)
        {
            ct.ThrowIfCancellationRequested();
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
            }
            yield return item;
        }
    }

    private static async IAsyncEnumerable<T> ThrowingAsyncEnumerableAsync<T>(
        Exception exception,
        int yieldCountBeforeThrow = 0,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (int i = 0; i < yieldCountBeforeThrow; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return default!;
        }
        await Task.Yield();
        throw exception;
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
    public async Task MergeAsync_WithSingleSource_ReturnsAllItemsAsync()
    {
        // Arrange
        IAsyncEnumerable<int>[] sources =
        [
            ToAsyncEnumerableAsync([1, 2, 3], ct: TestContext.Current.CancellationToken)
        ];

        // Act
        var result = await DrainAsync(
            StreamMerger.MergeAsync(sources, channelCapacity: 64, ct: TestContext.Current.CancellationToken));

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo([1, 2, 3]);
    }

    [Fact]
    public async Task MergeAsync_WithMultipleSources_MergesAllItemsAsync()
    {
        // Arrange
        IAsyncEnumerable<int>[] sources =
        [
            ToAsyncEnumerableAsync([1, 2, 3], ct: TestContext.Current.CancellationToken),
            ToAsyncEnumerableAsync([4, 5, 6], ct: TestContext.Current.CancellationToken),
            ToAsyncEnumerableAsync([7, 8, 9], ct: TestContext.Current.CancellationToken),
        ];

        // Act
        var result = await DrainAsync(
            StreamMerger.MergeAsync(sources, channelCapacity: 64, ct: TestContext.Current.CancellationToken));

        // Assert — all 9 items must be present; arrival order is not guaranteed
        result.Should().HaveCount(9);
        result.Should().BeEquivalentTo([1, 2, 3, 4, 5, 6, 7, 8, 9]);
    }

    [Fact]
    public async Task MergeAsync_WithEmptySources_ReturnsEmptyAsync()
    {
        // Arrange — zero sources and a single empty source
        IAsyncEnumerable<int>[] noSources = [];
        IAsyncEnumerable<int>[] emptySource =
        [
            ToAsyncEnumerableAsync(Array.Empty<int>(), ct: TestContext.Current.CancellationToken)
        ];

        // Act
        var resultNoSources = await DrainAsync(
            StreamMerger.MergeAsync(noSources, channelCapacity: 64, ct: TestContext.Current.CancellationToken));
        var resultEmptySource = await DrainAsync(
            StreamMerger.MergeAsync(emptySource, channelCapacity: 64, ct: TestContext.Current.CancellationToken));

        // Assert
        resultNoSources.Should().BeEmpty();
        resultEmptySource.Should().BeEmpty();
    }

    [Fact]
    public async Task MergeAsync_WithCancellation_StopsProductionAsync()
    {
        // Arrange — source produces items slowly; consumer cancels mid-way
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IAsyncEnumerable<int>[] sources =
        [
            ToAsyncEnumerableAsync(Enumerable.Range(1, 1000), delayMs: 20, ct: cts.Token)
        ];

        var collected = new List<int>();

        // Act
        var act = async () =>
        {
            await foreach (int item in StreamMerger.MergeAsync(sources, channelCapacity: 64, cts.Token)
                               .ConfigureAwait(false))
            {
                collected.Add(item);
                if (collected.Count >= 3)
                {
                    await cts.CancelAsync().ConfigureAwait(false);
                }
            }
        };

        // Assert — cancellation propagates as OperationCanceledException
        await act.Should().ThrowAsync<OperationCanceledException>();
        collected.Count.Should().BeLessThan(1000, "stream was cancelled before producing all items");
    }

    [Fact]
    public async Task MergeAsync_WithSourceException_PropagatesExceptionAsync()
    {
        // Arrange — one source yields 2 items then throws; a second source runs slowly
        var expectedException = new InvalidOperationException("source failure");
        IAsyncEnumerable<int>[] sources =
        [
            ThrowingAsyncEnumerableAsync<int>(expectedException, yieldCountBeforeThrow: 2,
                ct: TestContext.Current.CancellationToken),
            ToAsyncEnumerableAsync(Enumerable.Range(100, 100), delayMs: 5,
                ct: TestContext.Current.CancellationToken),
        ];

        // Act
        var act = async () =>
        {
            await foreach (int _ in StreamMerger.MergeAsync(sources, channelCapacity: 64,
                               TestContext.Current.CancellationToken).ConfigureAwait(false))
            {
                // consume items until exception propagates
            }
        };

        // Assert — when a source throws, the merged stream stops (either the original exception or
        // OperationCanceledException as the CTS is cancelled to stop the remaining producers).
        await act.Should().ThrowAsync<Exception>();
    }
}
