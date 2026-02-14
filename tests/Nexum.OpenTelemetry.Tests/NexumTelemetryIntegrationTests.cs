using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Nexum.OpenTelemetry.Tests;

[Trait("Category", "Integration")]
public sealed class NexumTelemetryIntegrationTests
{
    [Fact]
    public async Task AddNexumTelemetry_DispatchCommand_RecordsDispatchCountAndDurationAsync()
    {
        // Arrange
        var services = new ServiceCollection();

        // Mock all three dispatchers (AddNexumTelemetry decorates all three)
        var innerCommand = Substitute.For<ICommandDispatcher>();
        innerCommand.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>("result"));
        services.AddSingleton<ICommandDispatcher>(innerCommand);

        var innerQuery = Substitute.For<IQueryDispatcher>();
        services.AddSingleton<IQueryDispatcher>(innerQuery);

        var innerPublisher = Substitute.For<INotificationPublisher>();
        services.AddSingleton<INotificationPublisher>(innerPublisher);

        // Add telemetry decoration (defaults: both enabled)
        services.AddNexumTelemetry();

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        var instrumentation = serviceProvider.GetRequiredService<NexumInstrumentation>();

        using var countCollector = new MetricCollector<long>(instrumentation.DispatchCount);
        using var durationCollector = new MetricCollector<double>(instrumentation.DispatchDuration);

        var command = new TestCommand("test");

        // Act
        string result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.Should().Be("result");

        var countSnapshot = countCollector.GetMeasurementSnapshot();
        countSnapshot.Should().HaveCount(1);
        countSnapshot[0].Value.Should().Be(1);
        countSnapshot[0].Tags["type"].Should().Be("TestCommand");
        countSnapshot[0].Tags["status"].Should().Be("success");

        var durationSnapshot = durationCollector.GetMeasurementSnapshot();
        durationSnapshot.Should().HaveCount(1);
        durationSnapshot[0].Value.Should().BeGreaterThan(0.0);
        durationSnapshot[0].Tags["type"].Should().Be("TestCommand");
        durationSnapshot[0].Tags["status"].Should().Be("success");
    }

    [Fact]
    public async Task AddNexumTelemetry_DispatchQuery_RecordsDispatchMetricsAsync()
    {
        // Arrange
        var services = new ServiceCollection();

        // Mock all three dispatchers (AddNexumTelemetry decorates all three)
        var innerCommand = Substitute.For<ICommandDispatcher>();
        services.AddSingleton<ICommandDispatcher>(innerCommand);

        var innerQuery = Substitute.For<IQueryDispatcher>();
        innerQuery.DispatchAsync(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<string>("query-result"));
        services.AddSingleton<IQueryDispatcher>(innerQuery);

        var innerPublisher = Substitute.For<INotificationPublisher>();
        services.AddSingleton<INotificationPublisher>(innerPublisher);

        // Add telemetry decoration
        services.AddNexumTelemetry();

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();
        var instrumentation = serviceProvider.GetRequiredService<NexumInstrumentation>();

        using var countCollector = new MetricCollector<long>(instrumentation.DispatchCount);
        using var durationCollector = new MetricCollector<double>(instrumentation.DispatchDuration);

        var query = new TestQuery("test");

        // Act
        string result = await dispatcher.DispatchAsync(query, CancellationToken.None);

        // Assert
        result.Should().Be("query-result");

        var countSnapshot = countCollector.GetMeasurementSnapshot();
        countSnapshot.Should().HaveCount(1);
        countSnapshot[0].Value.Should().Be(1);
        countSnapshot[0].Tags["type"].Should().Be("TestQuery");
        countSnapshot[0].Tags["status"].Should().Be("success");

        var durationSnapshot = durationCollector.GetMeasurementSnapshot();
        durationSnapshot.Should().HaveCount(1);
        durationSnapshot[0].Value.Should().BeGreaterThan(0.0);
        durationSnapshot[0].Tags["type"].Should().Be("TestQuery");
        durationSnapshot[0].Tags["status"].Should().Be("success");
    }

    [Fact]
    public async Task AddNexumTelemetry_PublishNotification_RecordsNotificationCountAsync()
    {
        // Arrange
        var services = new ServiceCollection();

        // Mock all three dispatchers (AddNexumTelemetry decorates all three)
        var innerCommand = Substitute.For<ICommandDispatcher>();
        services.AddSingleton<ICommandDispatcher>(innerCommand);

        var innerQuery = Substitute.For<IQueryDispatcher>();
        services.AddSingleton<IQueryDispatcher>(innerQuery);

        var innerPublisher = Substitute.For<INotificationPublisher>();
        services.AddSingleton<INotificationPublisher>(innerPublisher);

        // Add telemetry decoration
        services.AddNexumTelemetry();

        var serviceProvider = services.BuildServiceProvider();
        var publisher = serviceProvider.GetRequiredService<INotificationPublisher>();
        var instrumentation = serviceProvider.GetRequiredService<NexumInstrumentation>();

        using var countCollector = new MetricCollector<long>(instrumentation.NotificationCount);

        var notification = new TestNotification("test");

        // Act
        await publisher.PublishAsync(notification, PublishStrategy.Sequential, CancellationToken.None);

        // Assert
        var countSnapshot = countCollector.GetMeasurementSnapshot();
        countSnapshot.Should().HaveCount(1);
        countSnapshot[0].Value.Should().Be(1);
        countSnapshot[0].Tags["type"].Should().Be("TestNotification");
        countSnapshot[0].Tags["strategy"].Should().Be("Sequential");
    }

    [Fact]
    public void AddNexumTelemetry_WithBothDisabled_NoDecorationAppliedAsync()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register mock dispatchers (specific instances)
        var innerCommand = Substitute.For<ICommandDispatcher>();
        services.AddSingleton<ICommandDispatcher>(innerCommand);

        var innerQuery = Substitute.For<IQueryDispatcher>();
        services.AddSingleton<IQueryDispatcher>(innerQuery);

        var innerPublisher = Substitute.For<INotificationPublisher>();
        services.AddSingleton<INotificationPublisher>(innerPublisher);

        // Add telemetry with both disabled
        services.AddNexumTelemetry(opts =>
        {
            opts.EnableTracing = false;
            opts.EnableMetrics = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        // Assert
        dispatcher.Should().BeSameAs(innerCommand);
    }

    [Fact]
    public void AddNexumTelemetry_WithoutAddNexum_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        Action act = () => services.AddNexumTelemetry();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No registration found for ICommandDispatcher*");
    }

    private sealed record TestCommand(string Value) : ICommand<string>;
    private sealed record TestQuery(string Value) : IQuery<string>;
    private sealed record TestNotification(string Message) : INotification;
}
