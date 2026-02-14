using System.Diagnostics;
using Nexum.Abstractions;

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

        var inner = Substitute.For<IQueryDispatcher>();
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

        var inner = Substitute.For<IQueryDispatcher>();
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

        var inner = Substitute.For<IQueryDispatcher>();
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

        var inner = Substitute.For<IQueryDispatcher>();
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

        var inner = Substitute.For<IQueryDispatcher>();
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
