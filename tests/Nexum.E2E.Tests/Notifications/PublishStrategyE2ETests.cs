#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.Notifications;

[Trait("Category", "E2E")]
public sealed class PublishStrategyE2ETests
{
    // E2E-020: Sequential notification — 3 handlers execute in registration order, timing ~sum
    [Fact]
    public async Task PublishAsync_Sequential_HandlersExecuteInOrderWithSerialTiming()
    {
        // Arrange
        var log = new List<string>();
        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);
        });

        using var scope = host.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var sw = Stopwatch.StartNew();
        await publisher.PublishAsync(
            new ItemCreatedNotification(Guid.NewGuid(), "Test"),
            PublishStrategy.Sequential, ct);
        sw.Stop();

        // Assert — 3 handlers x 100ms each = ~300ms serial
        log.Should().HaveCount(3);
        log[0].Should().StartWith("Handler1:");
        log[1].Should().StartWith("Handler2:");
        log[2].Should().StartWith("Handler3:");
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(250, "sequential execution should take ~300ms");
    }

    // E2E-021: Parallel notification — all handlers run concurrently, timing ~max single handler
    [Fact]
    public async Task PublishAsync_Parallel_HandlersCompleteFasterThanSequential()
    {
        // Arrange
        var log = new List<string>();
        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);
        });

        using var scope = host.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var sw = Stopwatch.StartNew();
        await publisher.PublishAsync(
            new ItemCreatedNotification(Guid.NewGuid(), "ParallelTest"),
            PublishStrategy.Parallel, ct);
        sw.Stop();

        // Assert — parallel should complete in ~100ms (max single handler), not 300ms
        log.Should().HaveCount(3);
        sw.ElapsedMilliseconds.Should().BeLessThan(250, "parallel execution should be faster than sequential");
    }

    // E2E-022: StopOnException — handler 2 throws, handler 3 does NOT execute
    [Fact]
    public async Task PublishAsync_StopOnException_StopsAtFirstFailure()
    {
        // Arrange — single List<string> shared between handlers and exception handler
        var log = new List<string>();
        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);
            services.AddNexumExceptionHandler<FaultyNotificationExceptionHandler>();
        });

        using var scope = host.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        var act = async () => await publisher.PublishAsync(
            new FaultyNotification("stop-test"),
            PublishStrategy.StopOnException, ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*FaultyHandler2*");

        // Handler1 executed, Handler2 threw, Handler3 did NOT execute
        log.Should().Contain("FaultyHandler1:ok");
        log.Should().Contain("FaultyHandler2:throwing");
        log.Should().NotContain("FaultyHandler3:ok");
    }

    // E2E-023: FireAndForget — PublishAsync returns immediately, handlers run in background
    [Fact]
    public async Task PublishAsync_FireAndForget_ReturnsImmediately()
    {
        // Arrange
        var log = new List<string>();
        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);
        });

        // Must start host for BackgroundService (NotificationBackgroundService)
        await host.StartAsync(TestContext.Current.CancellationToken);

        try
        {
            using var scope = host.Services.CreateScope();
            var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
            var ct = TestContext.Current.CancellationToken;

            // Act
            var sw = Stopwatch.StartNew();
            await publisher.PublishAsync(
                new ItemCreatedNotification(Guid.NewGuid(), "FireForget"),
                PublishStrategy.FireAndForget, ct);
            sw.Stop();

            // Assert — PublishAsync returns near-instantly
            sw.ElapsedMilliseconds.Should().BeLessThan(100, "FireAndForget should return immediately");

            // Wait for background processing
            await Task.Delay(500, ct);
            log.Should().HaveCount(3, "all 3 handlers should have executed in background");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    // E2E-025: No handlers registered — publish is a no-op, no exception
    [Fact]
    public async Task PublishAsync_NoHandlers_CompletesWithoutException()
    {
        // Arrange
        using var host = NexumTestHost.CreateHost();
        using var scope = host.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();
        var ct = TestContext.Current.CancellationToken;

        // Act — notification type with no registered handlers
        var act = async () => await publisher.PublishAsync(
            new NoHandlerNotification(), PublishStrategy.Sequential, ct);

        // Assert — no-op, no exception
        await act.Should().NotThrowAsync();
    }
}

file sealed record NoHandlerNotification : INotification;
