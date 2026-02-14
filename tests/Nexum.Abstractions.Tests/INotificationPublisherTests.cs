namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class INotificationPublisherTests
{
    private record TestNotification(string Message) : INotification;

    private class MockNotificationPublisher : INotificationPublisher
    {
        public ValueTask PublishAsync<TNotification>(
            TNotification notification,
            PublishStrategy? strategy = null,
            CancellationToken ct = default) where TNotification : INotification
            => ValueTask.CompletedTask;
    }

    [Fact]
    public void MockImplementation_SatisfiesInterface()
    {
        var publisher = new MockNotificationPublisher();

        publisher.Should().BeAssignableTo<INotificationPublisher>();
    }

    [Fact]
    public async Task PublishAsync_WithNotification_CompletesAsync()
    {
        var publisher = new MockNotificationPublisher();
        var notification = new TestNotification("test");

        await publisher.PublishAsync(notification, ct: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WithNullStrategy_UsesDefaultAsync()
    {
        var publisher = new MockNotificationPublisher();
        var notification = new TestNotification("test");

        await publisher.PublishAsync(notification, strategy: null, ct: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_WithAllStrategies_CompletesAsync()
    {
        var publisher = new MockNotificationPublisher();
        var notification = new TestNotification("test");
        var ct = TestContext.Current.CancellationToken;

        await publisher.PublishAsync(notification, PublishStrategy.Sequential, ct);
        await publisher.PublishAsync(notification, PublishStrategy.Parallel, ct);
        await publisher.PublishAsync(notification, PublishStrategy.StopOnException, ct);
        await publisher.PublishAsync(notification, PublishStrategy.FireAndForget, ct);
    }

    [Fact]
    public void PublishAsync_EnforcesINotificationConstraint()
    {
        var notification = new TestNotification("test");

        notification.Should().BeAssignableTo<INotification>();
    }
}
