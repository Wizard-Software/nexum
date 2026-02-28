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

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
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

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
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

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
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

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
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

    [Fact]
    public void Constructor_WhenInnerDoesNotImplementIInterceptableDispatcher_ThrowsInvalidOperationException()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        // A plain ICommandDispatcher mock that does NOT implement IInterceptableDispatcher
        var inner = Substitute.For<ICommandDispatcher>();

        // Act
        Action act = () => _ = new TracingCommandDispatcher(inner, options, instrumentation);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_WithTracingEnabled_CreatesActivityAsync()
    {
        // Arrange
        const string ExpectedResult = "intercepted-result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true,
            EnableMetrics = false
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        // Create a mock implementing BOTH ICommandDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .DispatchInterceptedCommandAsync(
                Arg.Any<TestCommand>(),
                Arg.Any<Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");
        Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>> pipeline =
            (_, _, _) => new ValueTask<string>(ExpectedResult);

        // Act
        string result = await ((IInterceptableDispatcher)sut)
            .DispatchInterceptedCommandAsync(command, pipeline, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Command TestCommand");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("nexum.command.type").Should().Be("TestCommand");
        activity.GetTagItem("nexum.command.result_type").Should().Be("String");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_WithMetricsEnabled_RecordsMetricsAsync()
    {
        // Arrange
        const string ExpectedResult = "intercepted-result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = false,
            EnableMetrics = true
        };
        using var instrumentation = new NexumInstrumentation(options);
        using var collector = new MetricCollector<long>(instrumentation.DispatchCount);

        // Create a mock implementing BOTH ICommandDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .DispatchInterceptedCommandAsync(
                Arg.Any<TestCommand>(),
                Arg.Any<Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");
        Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>> pipeline =
            (_, _, _) => new ValueTask<string>(ExpectedResult);

        // Act
        string result = await ((IInterceptableDispatcher)sut)
            .DispatchInterceptedCommandAsync(command, pipeline, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);

        CollectedMeasurement<long>[] measurements = collector.GetMeasurementSnapshot().ToArray();
        measurements.Should().HaveCount(1);
        measurements[0].Value.Should().Be(1);

        string? typeTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "type").Value as string;
        typeTag.Should().Be("TestCommand");

        string? statusTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "status").Value as string;
        statusTag.Should().Be("success");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_WithBothDisabled_DelegatesDirectlyToInnerAsync()
    {
        // Arrange
        const string ExpectedResult = "intercepted-result";
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

        // Create a mock implementing BOTH ICommandDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .DispatchInterceptedCommandAsync(
                Arg.Any<TestCommand>(),
                Arg.Any<Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var command = new TestCommand("test");
        Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>> pipeline =
            (_, _, _) => new ValueTask<string>(ExpectedResult);

        // Act
        string result = await ((IInterceptableDispatcher)sut)
            .DispatchInterceptedCommandAsync(command, pipeline, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Should().BeEmpty();

        await ((IInterceptableDispatcher)inner).Received(1).DispatchInterceptedCommandAsync(
            command,
            Arg.Any<Func<TestCommand, IServiceProvider, CancellationToken, ValueTask<string>>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DispatchInterceptedQueryAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var interceptable = (IInterceptableDispatcher)sut;

        // Act
        Action act = () => interceptable.DispatchInterceptedQueryAsync<TestQuery, string>(
            new TestQuery("test"),
            (_, _, _) => new ValueTask<string>("result"),
            CancellationToken.None);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void StreamInterceptedAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        var inner = Substitute.For<ICommandDispatcher, IInterceptableDispatcher>();
        var sut = new TracingCommandDispatcher(inner, options, instrumentation);
        var interceptable = (IInterceptableDispatcher)sut;

        // Act
        Action act = () => interceptable.StreamInterceptedAsync<TestStreamQuery, int>(
            new TestStreamQuery(1),
            (_, _, _) => AsyncEnumerable.Empty<int>(),
            CancellationToken.None);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    private sealed record TestCommand(string Value) : ICommand<string>;
    private sealed record TestQuery(string Value) : IQuery<string>;
    private sealed record TestStreamQuery(int Count) : IStreamQuery<int>;

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
