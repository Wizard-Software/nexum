using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry.Tests;

[Trait("Category", "Unit")]
public sealed class TracingNotificationPublisherTests
{
    [Fact]
    public async Task PublishAsync_WithTracingEnabled_CreatesActivityWithNotificationTagsAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == uniqueSourceName,
            Sample = SampleActivity,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<INotificationPublisher>();
        var publisher = new TracingNotificationPublisher(inner, options, instrumentation);
        var notification = new TestNotification("test");

        // Act
        await publisher.PublishAsync(notification, PublishStrategy.Parallel, CancellationToken.None);

        // Assert
        activities.Should().HaveCount(1);
        var activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Notification TestNotification");
        activity.GetTagItem("nexum.notification.type").Should().Be("TestNotification");
        activity.GetTagItem("nexum.notification.strategy").Should().Be("Parallel");
        activity.Status.Should().Be(ActivityStatusCode.Ok);

        await inner.Received(1).PublishAsync(
            Arg.Any<TestNotification>(), Arg.Any<PublishStrategy?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithNullStrategy_SetsStrategyTagToDefaultAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == uniqueSourceName,
            Sample = SampleActivity,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<INotificationPublisher>();
        var publisher = new TracingNotificationPublisher(inner, options, instrumentation);
        var notification = new TestNotification("test");

        // Act
        await publisher.PublishAsync(notification, strategy: null, CancellationToken.None);

        // Assert
        activities.Should().HaveCount(1);
        var activity = activities[0];
        activity.GetTagItem("nexum.notification.strategy").Should().Be("Default");

        await inner.Received(1).PublishAsync(
            Arg.Any<TestNotification>(), Arg.Any<PublishStrategy?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WhenInnerThrows_SetsActivityStatusErrorAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == uniqueSourceName,
            Sample = SampleActivity,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<INotificationPublisher>();
        inner.PublishAsync(Arg.Any<TestNotification>(), Arg.Any<PublishStrategy?>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("publish error")));

        var publisher = new TracingNotificationPublisher(inner, options, instrumentation);
        var notification = new TestNotification("test");

        // Act
        var act = async () => await publisher.PublishAsync(notification, PublishStrategy.Sequential, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        activities.Should().HaveCount(1);
        var activity = activities[0];
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be("publish error");

        await inner.Received(1).PublishAsync(
            Arg.Any<TestNotification>(), Arg.Any<PublishStrategy?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_WithBothDisabled_DelegatesDirectlyToInnerAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = false,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == uniqueSourceName,
            Sample = SampleActivity,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<INotificationPublisher>();
        var publisher = new TracingNotificationPublisher(inner, options, instrumentation);
        var notification = new TestNotification("test");

        // Act
        await publisher.PublishAsync(notification, PublishStrategy.Parallel, CancellationToken.None);

        // Assert
        activities.Should().BeEmpty();

        await inner.Received(1).PublishAsync(
            Arg.Any<TestNotification>(), Arg.Any<PublishStrategy?>(), Arg.Any<CancellationToken>());
    }

    private sealed record TestNotification(string Message) : INotification;

    private static ActivitySamplingResult SampleActivity(ref ActivityCreationOptions<ActivityContext> _)
        => ActivitySamplingResult.AllData;
}
