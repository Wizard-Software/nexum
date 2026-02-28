using System.Diagnostics;
using Nexum.Abstractions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Nexum.OpenTelemetry.Tests;

[Trait("Category", "Unit")]
public sealed class TracingQueryDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithTracingEnabled_CreatesActivityWithQueryTagsAsync()
    {
        // Arrange
        const string ExpectedResult = "test-result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        inner.DispatchAsync(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestQuery("test-value");

        // Act
        string result = await sut.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Query TestQuery");
        activity.GetTagItem("nexum.query.type").Should().Be("TestQuery");
        activity.GetTagItem("nexum.query.result_type").Should().Be("String");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public async Task DispatchAsync_WhenInnerThrows_SetsActivityStatusErrorAsync()
    {
        // Arrange
        const string ErrorMessage = "Query execution failed";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        inner.DispatchAsync(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<string>>(_ => throw new InvalidOperationException(ErrorMessage));

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestQuery("test-value");

        // Act
        Func<Task> act = async () => await sut.DispatchAsync(query);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(ErrorMessage);
    }

    [Fact]
    public async Task StreamAsync_WithTracingEnabled_CreatesActivityForStreamAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        inner.StreamAsync(Arg.Any<IStreamQuery<int>>(), Arg.Any<CancellationToken>())
            .Returns(ProduceItemsAsync());

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestStreamQuery(2);

        // Act
        var results = new List<int>();
        await foreach (int item in sut.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert
        results.Should().Equal(1, 2);
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Stream TestStreamQuery");
        activity.GetTagItem("nexum.query.type").Should().Be("TestStreamQuery");
        activity.GetTagItem("nexum.query.result_type").Should().Be("Int32");
        activity.Status.Should().Be(ActivityStatusCode.Ok);

        static async IAsyncEnumerable<int> ProduceItemsAsync()
        {
            yield return 1;
            yield return 2;
        }
    }

    [Fact]
    public async Task StreamAsync_WhenStreamFails_SetsActivityStatusErrorAsync()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = true
        };
        using var instrumentation = new NexumInstrumentation(options);

        var activities = new List<Activity>();
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        inner.StreamAsync(Arg.Any<IStreamQuery<int>>(), Arg.Any<CancellationToken>())
            .Returns(ProduceItemsThenThrowAsync());

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestStreamQuery(2);

        // Act
        Func<Task> act = async () =>
        {
            await foreach (int _ in sut.StreamAsync(query, TestContext.Current.CancellationToken))
            {
                // Enumerate to trigger the exception
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.Status.Should().Be(ActivityStatusCode.Error);

        static async IAsyncEnumerable<int> ProduceItemsThenThrowAsync()
        {
            yield return 1;
            throw new InvalidOperationException("Stream failed");
        }
    }

    [Fact]
    public async Task StreamAsync_WithBothDisabled_DelegatesDirectlyToInnerAsync()
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
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        inner.StreamAsync(Arg.Any<IStreamQuery<int>>(), Arg.Any<CancellationToken>())
            .Returns(ProduceItemsAsync());

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestStreamQuery(2);

        // Act
        var results = new List<int>();
        await foreach (int item in sut.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert
        results.Should().Equal(1, 2);
        activities.Should().BeEmpty();

        static async IAsyncEnumerable<int> ProduceItemsAsync()
        {
            yield return 1;
            yield return 2;
        }
    }

    [Fact]
    public void Constructor_WhenInnerDoesNotImplementIInterceptableDispatcher_ThrowsInvalidOperationException()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        // A plain IQueryDispatcher mock that does NOT implement IInterceptableDispatcher
        var inner = Substitute.For<IQueryDispatcher>();

        // Act
        Action act = () => _ = new TracingQueryDispatcher(inner, options, instrumentation);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_WithTracingEnabled_CreatesActivityAsync()
    {
        // Arrange
        const string ExpectedResult = "intercepted-query-result";
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

        // Create a mock implementing BOTH IQueryDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .DispatchInterceptedQueryAsync(
                Arg.Any<TestQuery>(),
                Arg.Any<Func<TestQuery, IServiceProvider, CancellationToken, ValueTask<string>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestQuery("test-value");
        Func<TestQuery, IServiceProvider, CancellationToken, ValueTask<string>> pipeline =
            (_, _, _) => new ValueTask<string>(ExpectedResult);

        // Act
        string result = await ((IInterceptableDispatcher)sut)
            .DispatchInterceptedQueryAsync(query, pipeline, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Query TestQuery");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("nexum.query.type").Should().Be("TestQuery");
        activity.GetTagItem("nexum.query.result_type").Should().Be("String");
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_WithMetricsEnabled_RecordsMetricsAsync()
    {
        // Arrange
        const string ExpectedResult = "intercepted-query-result";
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions
        {
            ActivitySourceName = uniqueSourceName,
            EnableTracing = false,
            EnableMetrics = true
        };
        using var instrumentation = new NexumInstrumentation(options);
        using var collector = new MetricCollector<long>(instrumentation.DispatchCount);

        // Create a mock implementing BOTH IQueryDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .DispatchInterceptedQueryAsync(
                Arg.Any<TestQuery>(),
                Arg.Any<Func<TestQuery, IServiceProvider, CancellationToken, ValueTask<string>>>(),
                Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>(ExpectedResult));

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestQuery("test-value");
        Func<TestQuery, IServiceProvider, CancellationToken, ValueTask<string>> pipeline =
            (_, _, _) => new ValueTask<string>(ExpectedResult);

        // Act
        string result = await ((IInterceptableDispatcher)sut)
            .DispatchInterceptedQueryAsync(query, pipeline, CancellationToken.None);

        // Assert
        result.Should().Be(ExpectedResult);

        CollectedMeasurement<long>[] measurements = collector.GetMeasurementSnapshot().ToArray();
        measurements.Should().HaveCount(1);
        measurements[0].Value.Should().Be(1);

        string? typeTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "type").Value as string;
        typeTag.Should().Be("TestQuery");

        string? statusTag = measurements[0].Tags.ToArray().FirstOrDefault(t => t.Key == "status").Value as string;
        statusTag.Should().Be("success");
    }

    [Fact]
    public async Task StreamInterceptedAsync_WithTracingEnabled_CreatesActivityAsync()
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
        using var listener = CreateActivityListener(uniqueSourceName, activities);
        ActivitySource.AddActivityListener(listener);

        // Create a mock implementing BOTH IQueryDispatcher AND IInterceptableDispatcher
        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        ((IInterceptableDispatcher)inner)
            .StreamInterceptedAsync(
                Arg.Any<TestStreamQuery>(),
                Arg.Any<Func<TestStreamQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<int>>>(),
                Arg.Any<CancellationToken>())
            .Returns(ProduceItemsAsync());

        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var query = new TestStreamQuery(2);
        Func<TestStreamQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<int>> pipeline =
            (_, _, _) => ProduceItemsAsync();

        // Act
        var results = new List<int>();
        await foreach (int item in ((IInterceptableDispatcher)sut)
            .StreamInterceptedAsync(query, pipeline, CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        results.Should().Equal(1, 2);
        activities.Should().HaveCount(1);

        Activity activity = activities[0];
        activity.DisplayName.Should().Be("Nexum.Stream TestStreamQuery");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
        activity.GetTagItem("nexum.query.type").Should().Be("TestStreamQuery");
        activity.GetTagItem("nexum.query.result_type").Should().Be("Int32");

        static async IAsyncEnumerable<int> ProduceItemsAsync()
        {
            yield return 1;
            yield return 2;
        }
    }

    [Fact]
    public void DispatchInterceptedCommandAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var uniqueSourceName = $"Test.{Guid.NewGuid()}";
        var options = new NexumTelemetryOptions { ActivitySourceName = uniqueSourceName };
        using var instrumentation = new NexumInstrumentation(options);

        var inner = Substitute.For<IQueryDispatcher, IInterceptableDispatcher>();
        var sut = new TracingQueryDispatcher(inner, options, instrumentation);
        var interceptable = (IInterceptableDispatcher)sut;

        // Act
        Action act = () => interceptable.DispatchInterceptedCommandAsync<TestCommand, string>(
            new TestCommand("test"),
            (_, _, _) => new ValueTask<string>("result"),
            CancellationToken.None);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    private sealed record TestQuery(string Value) : IQuery<string>;
    private sealed record TestStreamQuery(int Count) : IStreamQuery<int>;
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
