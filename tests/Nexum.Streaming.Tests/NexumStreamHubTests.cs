#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode for test usage
#pragma warning disable IL2067 // Suppress RequiresDynamicCode for test usage

using Nexum.Abstractions;

namespace Nexum.Streaming.Tests;

[Trait("Category", "Integration")]
public sealed class NexumStreamHubTests
{
    // -------------------------------------------------------------------------
    // Test types
    // -------------------------------------------------------------------------

    private sealed record TestStreamQuery(string Value) : IStreamQuery<string>;
    private sealed record TestStreamNotification(string Value) : IStreamNotification<string>;

    /// <summary>
    /// Concrete test hub that exposes protected base methods as public for testing.
    /// Mirrors the runtime path (Path 1) that user code would follow.
    /// </summary>
    private sealed class TestStreamHub(
        IQueryDispatcher queryDispatcher,
        IStreamNotificationPublisher notificationPublisher)
        : NexumStreamHubBase(queryDispatcher, notificationPublisher)
    {
        public IAsyncEnumerable<TResult> TestStreamQuery<TQuery, TResult>(
            TQuery query, CancellationToken ct)
            where TQuery : IStreamQuery<TResult>
            => StreamQueryAsync<TQuery, TResult>(query, ct);

        public IAsyncEnumerable<TItem> TestStreamNotification<TNotification, TItem>(
            TNotification notification, CancellationToken ct)
            where TNotification : IStreamNotification<TItem>
            => StreamNotificationAsync<TNotification, TItem>(notification, ct);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> source)
    {
        var result = new List<T>();
        await foreach (T item in source.ConfigureAwait(false))
        {
            result.Add(item);
        }
        return result;
    }

    private static async IAsyncEnumerable<T> YieldItemsAsync<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
        }
        await Task.CompletedTask.ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Constructor guard tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithNullQueryDispatcher_ThrowsArgumentNullException()
    {
        // Arrange
        var publisher = Substitute.For<IStreamNotificationPublisher>();

        // Act
        var act = () => new TestStreamHub(null!, publisher);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("queryDispatcher");
    }

    [Fact]
    public void Constructor_WithNullNotificationPublisher_ThrowsArgumentNullException()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();

        // Act
        var act = () => new TestStreamHub(dispatcher, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationPublisher");
    }

    // -------------------------------------------------------------------------
    // StreamQueryAsync tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StreamQueryAsync_WithRegisteredHandler_ReturnsStreamAsync()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();

        string[] expectedItems = ["order-1", "order-2", "order-3"];
        var query = new TestStreamQuery("customer-42");

        dispatcher.StreamAsync<string>(query, Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(expectedItems));

        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var result = await DrainAsync(
            hub.TestStreamQuery<TestStreamQuery, string>(query, TestContext.Current.CancellationToken));

        // Assert
        result.Should().BeEquivalentTo(expectedItems, options => options.WithStrictOrdering());
        dispatcher.Received(1).StreamAsync<string>(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamQueryAsync_WithEmptyStream_ReturnsEmptySequenceAsync()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();
        var query = new TestStreamQuery("no-results");

        dispatcher.StreamAsync<string>(query, Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(Array.Empty<string>()));

        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var result = await DrainAsync(
            hub.TestStreamQuery<TestStreamQuery, string>(query, TestContext.Current.CancellationToken));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void StreamQueryAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();
        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var act = () => hub.TestStreamQuery<TestStreamQuery, string>(null!, CancellationToken.None);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("query");
    }

    // -------------------------------------------------------------------------
    // StreamNotificationAsync tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StreamNotificationAsync_WithRegisteredHandler_ReturnsStreamAsync()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();

        string[] expectedItems = ["event-a", "event-b"];
        var notification = new TestStreamNotification("topic-1");

        publisher.StreamAsync<TestStreamNotification, string>(notification, Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(expectedItems));

        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var result = await DrainAsync(
            hub.TestStreamNotification<TestStreamNotification, string>(
                notification, TestContext.Current.CancellationToken));

        // Assert
        result.Should().BeEquivalentTo(expectedItems, options => options.WithStrictOrdering());
        publisher.Received(1).StreamAsync<TestStreamNotification, string>(
            notification, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StreamNotificationAsync_WithEmptyStream_ReturnsEmptySequenceAsync()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();
        var notification = new TestStreamNotification("silent-topic");

        publisher.StreamAsync<TestStreamNotification, string>(notification, Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(Array.Empty<string>()));

        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var result = await DrainAsync(
            hub.TestStreamNotification<TestStreamNotification, string>(
                notification, TestContext.Current.CancellationToken));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void StreamNotificationAsync_WithNullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        var dispatcher = Substitute.For<IQueryDispatcher>();
        var publisher = Substitute.For<IStreamNotificationPublisher>();
        var hub = new TestStreamHub(dispatcher, publisher);

        // Act
        var act = () => hub.TestStreamNotification<TestStreamNotification, string>(
            null!, CancellationToken.None);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notification");
    }
}
