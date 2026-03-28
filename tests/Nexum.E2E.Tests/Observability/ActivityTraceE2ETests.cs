#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;
using Nexum.OpenTelemetry;

namespace Nexum.E2E.Tests.Observability;

[Trait("Category", "E2E")]
public sealed class ActivityTraceE2ETests : IDisposable
{
    private readonly List<Activity> _startedActivities = [];
    private readonly List<Activity> _stoppedActivities = [];
    private readonly ActivityListener _listener;
    private readonly IHost _host;

    public ActivityTraceE2ETests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Nexum.Cqrs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _startedActivities.Add(activity),
            ActivityStopped = activity => _stoppedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);

        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddNexumTelemetry(opts =>
            {
                opts.EnableTracing = true;
                opts.EnableMetrics = true;
            });
        });
    }

    public void Dispose()
    {
        _listener.Dispose();
        _host.Dispose();
    }

    // E2E-080: Dispatch command -> Activity created with DisplayName containing command type name
    [Fact]
    public async Task DispatchAsync_Command_CreatesActivityWithCommandTypeName()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await dispatcher.DispatchAsync(new CreateItemCommand("Traced"), ct);

        // Assert
        _startedActivities.Should().ContainSingle(a => a.DisplayName.Contains("CreateItemCommand"));
    }

    // E2E-081: Dispatch query -> Activity created with DisplayName containing query type name
    [Fact]
    public async Task DispatchAsync_Query_CreatesActivityWithQueryTypeName()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await dispatcher.DispatchAsync(new GetItemQuery(Guid.NewGuid()), ct);

        // Assert
        _startedActivities.Should().ContainSingle(a => a.DisplayName.Contains("GetItemQuery"));
    }

    // E2E-082: Dispatch with behavior -> single Activity spans entire pipeline
    [Fact]
    public async Task DispatchAsync_CommandWithBehavior_SingleActivitySpansEntirePipeline()
    {
        // Arrange
        var log = new List<string>();
        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);
            services.AddNexumBehavior(typeof(TrackingCommandBehavior));
            services.AddNexumTelemetry(opts => opts.EnableTracing = true);
        });

        var localStopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Nexum.Cqrs",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => localStopped.Add(a),
        };
        ActivitySource.AddActivityListener(listener);

        using var scope = host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        await dispatcher.DispatchAsync(new TrackedCommand("traced"), ct);

        // Assert - single activity for the full dispatch, behavior executed within it
        localStopped.Should().ContainSingle(a => a.DisplayName.Contains("TrackedCommand"));
        log.Should().Contain("Behavior:Before");
        log.Should().Contain("Behavior:After");
    }

    // E2E-083: Dispatch failing command -> Activity records error status
    [Fact]
    public async Task DispatchAsync_FailingCommand_ActivityRecordsException()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        try
        {
            await dispatcher.DispatchAsync(new FailingCommand("trace error"), ct);
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert - Activity should have error status
        _stoppedActivities.Should().ContainSingle(a => a.DisplayName.Contains("FailingCommand"));
        var activity = _stoppedActivities.First(a => a.DisplayName.Contains("FailingCommand"));
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }
}
