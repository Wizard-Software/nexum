using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Channels;
using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexum.Internal;

/// <summary>
/// Background service that processes notifications queued via <see cref="PublishStrategy.FireAndForget"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implements ADR-004: Uses a bounded channel to queue notifications and processes them
/// in a separate background thread. Each notification is processed in its own DI scope
/// with a per-notification timeout controlled by <see cref="NexumOptions.FireAndForgetTimeout"/>.
/// </para>
/// <para>
/// <see cref="ExecutionContext"/> is propagated from the publisher to the background service
/// to preserve ambient data (AsyncLocal values like user identity, Activity/trace context).
/// </para>
/// <para>
/// The service is designed to never crash — all exceptions are routed through exception handlers
/// and logged. If a handler throws, the exception handler is invoked, and processing continues
/// with the next handler.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="NotificationBackgroundService"/> class.
/// </remarks>
/// <param name="reader">The channel reader for dequeuing notifications.</param>
/// <param name="scopeFactory">Factory for creating DI scopes (one per notification).</param>
/// <param name="exceptionHandlerResolver">Resolver for exception handlers.</param>
/// <param name="options">Runtime configuration options.</param>
/// <param name="logger">Logger instance.</param>
internal sealed class NotificationBackgroundService(
    ChannelReader<NotificationEnvelope> reader,
    IServiceScopeFactory scopeFactory,
    ExceptionHandlerResolver exceptionHandlerResolver,
    NexumOptions options,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    private readonly ChannelReader<NotificationEnvelope> _reader = reader;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ExceptionHandlerResolver _exceptionHandlerResolver = exceptionHandlerResolver;
    private readonly NexumOptions _options = options;
    private readonly ILogger<NotificationBackgroundService> _logger = logger;

    /// <summary>
    /// Cache mapping notification type to the closed <see cref="INotificationHandler{TNotification}"/> type.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, Type> s_handlerTypeCache = new();

    /// <summary>
    /// Cache mapping closed handler interface type to its <c>HandleAsync</c> <see cref="MethodInfo"/>.
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handleAsyncMethodCache = new();

    /// <summary>
    /// Executes the background service, reading notifications from the channel and processing them.
    /// </summary>
    /// <param name="stoppingToken">Token signaling application shutdown.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NotificationBackgroundService started");

        try
        {
            await foreach (NotificationEnvelope envelope in _reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await ProcessNotificationAsync(envelope, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown — expected during application stop
            _logger.LogInformation("NotificationBackgroundService is shutting down");
        }
        catch (Exception ex)
        {
            // Should never happen — ProcessNotificationAsync catches all exceptions
            _logger.LogCritical(ex, "Unexpected exception in NotificationBackgroundService main loop");
        }
    }

    /// <summary>
    /// Processes a single notification envelope with timeout and ExecutionContext restoration.
    /// </summary>
    /// <param name="envelope">The notification envelope to process.</param>
    /// <param name="stoppingToken">Token signaling application shutdown.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method MUST NOT throw exceptions to the caller — the background service must never crash.
    /// All exceptions are logged and/or routed through exception handlers.
    /// </remarks>
    private async Task ProcessNotificationAsync(NotificationEnvelope envelope, CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(_options.FireAndForgetTimeout);

        try
        {
            if (envelope.CapturedContext is not null)
            {
                await RunWithCapturedContextAsync(envelope.CapturedContext,
                    () => InvokeHandlersInScopeAsync(envelope, cts.Token)).ConfigureAwait(false);
            }
            else
            {
                await InvokeHandlersInScopeAsync(envelope, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Notification {NotificationType} timed out after {Timeout}",
                envelope.NotificationType.Name,
                _options.FireAndForgetTimeout);
        }
        catch (Exception ex)
        {
            // Background service must never crash — log and continue
            _logger.LogError(ex,
                "Unhandled exception processing notification {NotificationType}",
                envelope.NotificationType.Name);
        }
    }

    /// <summary>
    /// Executes an async function within a captured <see cref="ExecutionContext"/>,
    /// restoring ambient data (AsyncLocal values) for the duration of the call.
    /// </summary>
    /// <param name="context">The captured execution context.</param>
    /// <param name="asyncWork">The async work to execute within the restored context.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="ExecutionContext.Run"/> executes the callback synchronously with the restored context.
    /// The async work initiated inside <c>Run</c> inherits the restored ExecutionContext through
    /// normal .NET async flow (async/await continuations).
    /// </para>
    /// <para>
    /// This pattern preserves AsyncLocal values like user identity, Activity/trace context,
    /// and other ambient data from the publisher to the background handler.
    /// </para>
    /// </remarks>
    private static async Task RunWithCapturedContextAsync(ExecutionContext context, Func<Task> asyncWork)
    {
        Task? task = null;
        ExecutionContext.Run(context, state =>
        {
            var callback = (Func<Task>)state!;
            task = callback();
        }, asyncWork);

        if (task is not null)
        {
            await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Invokes all registered handlers for a notification within a new DI scope.
    /// </summary>
    /// <param name="envelope">The notification envelope to process.</param>
    /// <param name="ct">Cancellation token (linked with timeout).</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    private async Task InvokeHandlersInScopeAsync(NotificationEnvelope envelope, CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        Type closedHandlerType = s_handlerTypeCache.GetOrAdd(
            envelope.NotificationType,
            static t => typeof(INotificationHandler<>).MakeGenericType(t));

        IEnumerable<object?> handlers = scope.ServiceProvider.GetServices(closedHandlerType);

        foreach (object? handler in handlers)
        {
            if (handler is null)
            {
                continue;
            }

            try
            {
                await InvokeHandleAsyncAsync(handler, closedHandlerType, envelope.Notification, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    await _exceptionHandlerResolver.InvokeNotificationExceptionHandlersAsync(
                        envelope.Notification,
                        envelope.NotificationType,
                        ex,
                        ct).ConfigureAwait(false);
                }
                catch (Exception ehEx)
                {
                    _logger.LogWarning(ehEx,
                        "Exception handler threw while handling {ExceptionType} for notification {NotificationType}",
                        ex.GetType().Name,
                        envelope.NotificationType.Name);
                }

                // Continue processing remaining handlers — do NOT rethrow
                // (Background processing: exceptions are routed to exception handlers, not propagated)
            }
        }
    }

    /// <summary>
    /// Invokes a handler's <c>HandleAsync</c> method via reflection.
    /// </summary>
    /// <param name="handler">The handler instance.</param>
    /// <param name="closedHandlerType">The closed <see cref="INotificationHandler{TNotification}"/> type.</param>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Uses cached <see cref="MethodInfo"/> for performance. The reflection result is boxed as <see cref="object"/>,
    /// cast to <see cref="ValueTask"/>, and awaited.
    /// </remarks>
    private static async Task InvokeHandleAsyncAsync(
        object handler,
        Type closedHandlerType,
        INotification notification,
        CancellationToken ct)
    {
        MethodInfo methodInfo = s_handleAsyncMethodCache.GetOrAdd(closedHandlerType, static type =>
        {
            MethodInfo method = type.GetMethod("HandleAsync")
                ?? throw new InvalidOperationException(
                    $"Handler type '{type.FullName}' is missing HandleAsync method.");
            return method;
        });

        object? result = methodInfo.Invoke(handler, [notification, ct]);
        if (result is ValueTask valueTask)
        {
            await valueTask.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops the background service and attempts to drain remaining notifications
    /// within the configured <see cref="NexumOptions.FireAndForgetDrainTimeout"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token from the host.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Creates a linked cancellation token source that respects both the configured drain timeout
    /// and the host shutdown token, then delegates to <see cref="BackgroundService.StopAsync"/>.
    /// </remarks>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "NotificationBackgroundService stopping — attempting to drain channel within {DrainTimeout}",
            _options.FireAndForgetDrainTimeout);

        using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        drainCts.CancelAfter(_options.FireAndForgetDrainTimeout);

        // Let base.StopAsync cancel the ExecuteAsync via its internal CancellationTokenSource
        await base.StopAsync(drainCts.Token).ConfigureAwait(false);

        _logger.LogInformation("NotificationBackgroundService stopped");
    }
}
