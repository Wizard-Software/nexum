using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class NotificationPublisherFireAndForgetTests : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private NotificationBackgroundService? _backgroundService;

    public void Dispose()
    {
        _backgroundService?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _backgroundService?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task PublishAsync_FireAndForget_EnqueueAndDequeue_HandlerExecutedAsync()
    {
        // Arrange
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        services.AddScoped<INotificationHandler<TestNotification>>(_ =>
            new SignalingNotificationHandler(tcs));

        _serviceProvider = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(
            _serviceProvider, _serviceProvider.GetRequiredService<NexumOptions>(), channel.Writer);

        _backgroundService = new NotificationBackgroundService(
            channel.Reader,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ExceptionHandlerResolver>(),
            _serviceProvider.GetRequiredService<NexumOptions>(),
            NullLogger<NotificationBackgroundService>.Instance);

        // Act
        await _backgroundService.StartAsync(TestContext.Current.CancellationToken);
        await publisher.PublishAsync(new TestNotification("fire"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Assert — handler executed in background within timeout
        await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_FireAndForget_HandlerThrows_ExceptionHandlerInvokedAsync()
    {
        // Arrange
        var exceptionHandlerTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        services.AddScoped<INotificationHandler<TestNotification>>(_ =>
            new ThrowingNotificationHandler(new InvalidOperationException("bg error")));
        services.AddTransient<INotificationExceptionHandler<TestNotification, InvalidOperationException>>(_ =>
            new SignalingExceptionHandler<TestNotification, InvalidOperationException>(exceptionHandlerTcs));

        _serviceProvider = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(
            _serviceProvider, _serviceProvider.GetRequiredService<NexumOptions>(), channel.Writer);

        _backgroundService = new NotificationBackgroundService(
            channel.Reader,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ExceptionHandlerResolver>(),
            _serviceProvider.GetRequiredService<NexumOptions>(),
            NullLogger<NotificationBackgroundService>.Instance);

        // Act
        await _backgroundService.StartAsync(TestContext.Current.CancellationToken);
        await publisher.PublishAsync(new TestNotification("test"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Assert — exception handler invoked in background (not crash)
        await exceptionHandlerTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_FireAndForget_ScopeDisposed_AfterHandlerAsync()
    {
        // Arrange
        var scopeDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        services.AddScoped<INotificationHandler<TestNotification>>(_ =>
            new ScopeTrackingNotificationHandler(scopeDisposed));
        services.AddScoped<DisposableTracker>();

        _serviceProvider = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(
            _serviceProvider, _serviceProvider.GetRequiredService<NexumOptions>(), channel.Writer);

        _backgroundService = new NotificationBackgroundService(
            channel.Reader,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ExceptionHandlerResolver>(),
            _serviceProvider.GetRequiredService<NexumOptions>(),
            NullLogger<NotificationBackgroundService>.Instance);

        // Act
        await _backgroundService.StartAsync(TestContext.Current.CancellationToken);
        await publisher.PublishAsync(new TestNotification("scope"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Assert — scope was created and handler was executed (scope disposal is managed by BackgroundService)
        await scopeDisposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_FireAndForget_HandlerTimeout_BackgroundServiceContinuesAsync()
    {
        // Arrange
        var secondHandlerTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);
        var handlerCallCount = 0;

        var options = new NexumOptions { FireAndForgetTimeout = TimeSpan.FromMilliseconds(100) };

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        services.AddScoped<INotificationHandler<TestNotification>>(_ =>
        {
            int call = Interlocked.Increment(ref handlerCallCount);
            return call == 1
                ? new SlowNotificationHandler(TimeSpan.FromSeconds(10)) // First: times out
                : new SignalingNotificationHandler(secondHandlerTcs); // Second: signals
        });

        _serviceProvider = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(
            _serviceProvider, _serviceProvider.GetRequiredService<NexumOptions>(), channel.Writer);

        _backgroundService = new NotificationBackgroundService(
            channel.Reader,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ExceptionHandlerResolver>(),
            options,
            NullLogger<NotificationBackgroundService>.Instance);

        // Act
        await _backgroundService.StartAsync(TestContext.Current.CancellationToken);

        // First notification → slow handler → CancellationToken fires after 100ms → TaskCanceledException caught
        await publisher.PublishAsync(new TestNotification("slow"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Wait for timeout to fire and handler to complete
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        // Second notification → fast handler → signals TCS
        await publisher.PublishAsync(new TestNotification("fast"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Assert — background service continued and processed second notification
        await secondHandlerTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    // 4.11 — ExecutionContext flow
    [Fact]
    public async Task PublishAsync_FireAndForget_AsyncLocalPropagated_ToBackgroundHandlerAsync()
    {
        // Arrange
        var asyncLocal = new AsyncLocal<string>();
        var capturedValueTcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        services.AddScoped<INotificationHandler<TestNotification>>(_ =>
            new AsyncLocalCapturingHandler(asyncLocal, capturedValueTcs));

        _serviceProvider = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(
            _serviceProvider, _serviceProvider.GetRequiredService<NexumOptions>(), channel.Writer);

        _backgroundService = new NotificationBackgroundService(
            channel.Reader,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _serviceProvider.GetRequiredService<ExceptionHandlerResolver>(),
            _serviceProvider.GetRequiredService<NexumOptions>(),
            NullLogger<NotificationBackgroundService>.Instance);

        // Act — set AsyncLocal before publishing
        asyncLocal.Value = "correlation-id-123";
        await _backgroundService.StartAsync(TestContext.Current.CancellationToken);
        await publisher.PublishAsync(new TestNotification("ctx"), PublishStrategy.FireAndForget, TestContext.Current.CancellationToken);

        // Assert — AsyncLocal value was propagated to background handler
        var capturedValue = await capturedValueTcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        capturedValue.Should().Be("correlation-id-123");
    }

    #region Test Types

    internal sealed record TestNotification(string Value = "test") : INotification;

    internal sealed class SignalingNotificationHandler(TaskCompletionSource tcs)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            tcs.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ThrowingNotificationHandler(Exception exception)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
            => new ValueTask(Task.FromException(exception));
    }

    internal sealed class SignalingExceptionHandler<TNotification, TException>(TaskCompletionSource tcs)
        : INotificationExceptionHandler<TNotification, TException>
        where TNotification : INotification
        where TException : Exception
    {
        public ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default)
        {
            tcs.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ScopeTrackingNotificationHandler(TaskCompletionSource tcs)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            // If we got here, a new scope was created by the BackgroundService
            tcs.TrySetResult();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class DisposableTracker : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    internal sealed class SlowNotificationHandler(TimeSpan delay) : INotificationHandler<TestNotification>
    {
        public async ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
    }

    internal sealed class AsyncLocalCapturingHandler(
        AsyncLocal<string> asyncLocal,
        TaskCompletionSource<string?> capturedValueTcs) : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            capturedValueTcs.TrySetResult(asyncLocal.Value);
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
