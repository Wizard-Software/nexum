using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class NotificationPublisherParallelTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task PublishAsync_Parallel_ThreeHandlers_AllExecutedConcurrentlyAsync()
    {
        // Arrange
        var concurrencyCounter = new SemaphoreSlim(0, 3);
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var maxConcurrency = 0;
        var currentConcurrency = 0;

        var (publisher, sp) = CreatePublisher(services =>
        {
            for (int i = 0; i < 3; i++)
            {
                services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                    new ConcurrencyTrackingHandler(
                        concurrencyCounter,
                        barrier,
                        () => Interlocked.Increment(ref currentConcurrency),
                        () =>
                        {
                            int current = Volatile.Read(ref currentConcurrency);
                            int max;
                            do
                            {
                                max = Volatile.Read(ref maxConcurrency);
                            } while (current > max && Interlocked.CompareExchange(ref maxConcurrency, current, max) != max);
                        },
                        () => Interlocked.Decrement(ref currentConcurrency)));
            }
        });
        _serviceProvider = sp;

        var notification = new TestNotification("parallel");

        // Act — start publish in background, then release barrier once all handlers are waiting
        var ct = TestContext.Current.CancellationToken;
        var publishTask = Task.Run(
            async () => await publisher.PublishAsync(notification, PublishStrategy.Parallel, ct), ct);

        // Wait for all 3 handlers to signal they've entered
        for (int i = 0; i < 3; i++)
        {
            await concurrencyCounter.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }

        // All 3 handlers are now running concurrently — release them
        barrier.SetResult();

        await publishTask.WaitAsync(TimeSpan.FromSeconds(5), ct);

        // Assert — max concurrency should be 3 (all running simultaneously)
        maxConcurrency.Should().Be(3);
    }

    [Fact]
    public async Task PublishAsync_Parallel_OneHandlerThrows_ThrowsDirectlyAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H1"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("parallel error")));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act & Assert — single exception thrown directly (not AggregateException)
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Parallel, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("parallel error");
    }

    [Fact]
    public async Task PublishAsync_Parallel_TwoHandlersThrow_ThrowsAggregateExceptionAsync()
    {
        // Arrange
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("error1")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new ArgumentException("error2")));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act & Assert — ≥2 exceptions → AggregateException
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Parallel, TestContext.Current.CancellationToken);
        var ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task PublishAsync_Parallel_HandlerThrows_ExceptionHandlerInvokedPerHandlerAsync()
    {
        // Arrange
        var exceptionHandlerLog = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("error1")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("error2")));
            services.AddTransient<INotificationExceptionHandler<TestNotification, InvalidOperationException>>(_ =>
                new TrackingExceptionHandler<TestNotification, InvalidOperationException>(exceptionHandlerLog));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Parallel, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<AggregateException>();

        // Assert — exception handler invoked once per handler exception
        exceptionHandlerLog.Should().HaveCount(2);
    }

    #region Helper Methods

    private static (NotificationPublisher Publisher, ServiceProvider ServiceProvider) CreatePublisher(
        Action<IServiceCollection>? configure = null)
    {
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        configure?.Invoke(services);
        var sp = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(sp, sp.GetRequiredService<NexumOptions>(), channel.Writer);
        return (publisher, sp);
    }

    #endregion

    #region Test Types

    internal sealed record TestNotification(string Value = "test") : INotification;

    internal sealed class TrackingNotificationHandler(List<string> log, string name)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            log.Add($"{name}:{notification.Value}");
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ThrowingNotificationHandler(Exception exception)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
            => new ValueTask(Task.FromException(exception));
    }

    internal sealed class ConcurrencyTrackingHandler(
        SemaphoreSlim entrySignal,
        TaskCompletionSource barrier,
        Action onEnter,
        Action onUpdateMax,
        Action onExit) : INotificationHandler<TestNotification>
    {
        public async ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            onEnter();
            onUpdateMax();
            entrySignal.Release();
            await barrier.Task.ConfigureAwait(false);
            onExit();
        }
    }

    internal sealed class TrackingExceptionHandler<TNotification, TException>(List<string> log)
        : INotificationExceptionHandler<TNotification, TException>
        where TNotification : INotification
        where TException : Exception
    {
        public ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default)
        {
            log.Add($"{typeof(TException).Name}:{exception.Message}");
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
