using System.Diagnostics;
using Nexum.Abstractions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Nexum.OpenTelemetry.Tests;

[Trait("Category", "Unit")]
public sealed class TracingCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithTracingEnabled_CreatesActivityWithCorrectTagsAsync()
    {
        // Arrange
        const string ExpectedResult = "result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<ICommandDispatcher>();
        inner.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");

        // Act
        string result = await sut.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Count.Should().Be(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Command TestCommand");
        activity.Status.Should().Be(ActivityStatusCode.Ok);

        string? commandTypeTag = activity.Tags.FirstOrDefault(t => t.Key == "nexum.command.type").Value;
        commandTypeTag.Should().Be("TestCommand");

        string? resultTypeTag = activity.Tags.FirstOrDefault(t => t.Key == "nexum.command.result_type").Value;
        resultTypeTag.Should().Be("String");
    }

    [Fact]
    public async Task DispatchAsync_WhenInnerThrows_SetsActivityStatusErrorAsync()
    {
        // Arrange
        const string ErrorMessage = "test error";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<ICommandDispatcher>();
        var exception = new InvalidOperationException(ErrorMessage);
        inner.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<string>(exception));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");

        // Act
        Func<Task> act = async () => await sut.DispatchAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(ErrorMessage);
        activities.Count.Should().Be(1);

        Activity activity = activities[0];
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Contain(ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_WithBothTracingAndMetricsDisabled_DelegatesDirectlyToInnerAsync()
    {
        // Arrange
        const string ExpectedResult = "result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = false,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<ICommandDispatcher>();
        inner.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");

        // Act
        string result = await sut.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        await inner.Received(1).DispatchAsync(command, Arg.Any<CancellationToken>());
        activities.Count.Should().Be(0);
    }

    [Fact]
    public async Task DispatchAsync_WithOnlyMetricsEnabled_RecordsMetricsWithoutActivityAsync()
    {
        // Arrange
        const string ExpectedResult = "result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = false,
            EnableMetrics = true
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        using var collector = new MetricCollector<long>(instrumentation.DispatchCount);

        var inner = Substitute.For<ICommandDispatcher>();
        inner.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");

        // Act
        string result = await sut.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Count.Should().Be(0);

        CollectedMeasurement<long>[] measurements = collector.GetMeasurementSnapshot().ToArray();
        measurements.Length.Should().Be(1);
        measurements[0].Value.Should().Be(1);

        string? typeTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "type").Value as string;
        typeTag.Should().Be("TestCommand");

        string? statusTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "status").Value as string;
        statusTag.Should().Be("success");
    }

    private sealed record TestCommand(string Value) : ICommand<string>;

    private static ActivityListener CreateActivityListener(string sourceName, List<Activity> activities)
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = SampleActivity,
            ActivityStopped = activities.Add
        };
    }

    private static ActivitySamplingResult SampleActivity(ref ActivityCreationOptions<ActivityContext> _)
        => ActivitySamplingResult.AllData;
}
