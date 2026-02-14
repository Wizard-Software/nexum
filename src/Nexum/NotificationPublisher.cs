using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum;

/// <summary>
/// Default implementation of <see cref="INotificationPublisher"/> that publishes notifications
/// to their handlers using one of four strategies.
/// </summary>
/// <remarks>
/// <para>
/// This publisher is thread-safe and designed to be registered as a singleton.
/// </para>
/// <para>
/// The publish flow:
/// </para>
/// <list type="number">
/// <item>Resolve all handlers for the notification type via <c>IEnumerable&lt;INotificationHandler&lt;T&gt;&gt;</c></item>
/// <item>If no handlers are registered, return immediately (no-op)</item>
/// <item>Dispatch to the appropriate strategy method based on the effective strategy (or <see cref="NexumOptions.DefaultPublishStrategy"/>)</item>
/// <item>For synchronous strategies (Sequential, Parallel, StopOnException): invoke exception handlers on failure (always re-throws)</item>
/// <item>For FireAndForget: enqueue the notification to a bounded channel for background processing</item>
/// </list>
/// <para>
/// Exception handling semantics:
/// </para>
/// <list type="bullet">
/// <item><b>Single exception</b>: thrown directly (not wrapped in <see cref="AggregateException"/>)</item>
/// <item><b>Multiple exceptions</b>: wrapped in <see cref="AggregateException"/> with all inner exceptions</item>
/// <item><b>Exception handlers</b>: invoked for side-effects (logging, metrics) and always re-throw the original exception</item>
/// </list>
/// </remarks>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NexumOptions _options;
    private readonly ChannelWriter<NotificationEnvelope> _channelWriter;
    private readonly ExceptionHandlerResolver _exceptionHandlerResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers.</param>
    /// <param name="options">The Nexum runtime options.</param>
    /// <param name="channelWriter">The channel writer for FireAndForget strategy.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Constructor is internal because <paramref name="channelWriter"/> uses the internal
    /// <see cref="NotificationEnvelope"/> type. DI registration handles the instantiation.
    /// </remarks>
    internal NotificationPublisher(
        IServiceProvider serviceProvider,
        NexumOptions options,
        ChannelWriter<NotificationEnvelope> channelWriter)
    {
        _serviceProvider = serviceProvider
            ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        _channelWriter = channelWriter
            ?? throw new ArgumentNullException(nameof(channelWriter));
        _exceptionHandlerResolver = serviceProvider.GetRequiredService<ExceptionHandlerResolver>();
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync<TNotification>(
        TNotification notification,
        PublishStrategy? strategy = null,
        CancellationToken ct = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Resolve the effective strategy
        PublishStrategy effectiveStrategy = strategy ?? _options.DefaultPublishStrategy;

        // Resolve all handlers for this notification type
        IEnumerable<INotificationHandler<TNotification>> handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();

        // If no handlers are registered, return early (no-op)
        if (!handlers.Any())
        {
            return;
        }

        // Dispatch to the appropriate strategy
        switch (effectiveStrategy)
        {
            case PublishStrategy.Sequential:
                await PublishSequentialAsync(handlers, notification, ct).ConfigureAwait(false);
                break;

            case PublishStrategy.Parallel:
                await PublishParallelAsync(handlers, notification, ct).ConfigureAwait(false);
                break;

            case PublishStrategy.StopOnException:
                await PublishStopOnExceptionAsync(handlers, notification, ct).ConfigureAwait(false);
                break;

            case PublishStrategy.FireAndForget:
                await PublishFireAndForgetAsync(notification, ct).ConfigureAwait(false);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(strategy),
                    effectiveStrategy,
                    $"Unknown PublishStrategy: {effectiveStrategy}");
        }
    }

    /// <summary>
    /// Publishes the notification to handlers sequentially.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All handlers are invoked regardless of exceptions. Exceptions are collected and:
    /// </para>
    /// <list type="bullet">
    /// <item>If 1 handler throws: exception is propagated directly (not wrapped)</item>
    /// <item>If ≥2 handlers throw: exceptions are wrapped in <see cref="AggregateException"/></item>
    /// </list>
    /// <para>
    /// Exception handlers are invoked for each exception as a side-effect before propagation.
    /// </para>
    /// </remarks>
    private async ValueTask PublishSequentialAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken ct)
        where TNotification : INotification
    {
        List<Exception>? exceptions = null;

        foreach (INotificationHandler<TNotification> handler in handlers)
        {
            try
            {
                await handler.HandleAsync(notification, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Allocate the list only when the first exception occurs
                exceptions ??= [];
                exceptions.Add(ex);

                // Invoke exception handlers (side-effect only — always re-throws)
                await _exceptionHandlerResolver
                    .InvokeNotificationExceptionHandlersAsync(notification, ex, ct)
                    .ConfigureAwait(false);
            }
        }

        // Propagate exceptions using dual-exception pattern
        if (exceptions is not null)
        {
            if (exceptions.Count == 1)
            {
                throw exceptions[0]; // Single exception — throw directly
            }
            else
            {
                throw new AggregateException(exceptions); // Multiple exceptions — wrap
            }
        }
    }

    /// <summary>
    /// Publishes the notification to handlers sequentially, stopping at the first exception.
    /// </summary>
    /// <remarks>
    /// The first exception invokes exception handlers (side-effect) and is then propagated immediately.
    /// Remaining handlers are not invoked.
    /// </remarks>
    private async ValueTask PublishStopOnExceptionAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken ct)
        where TNotification : INotification
    {
        foreach (INotificationHandler<TNotification> handler in handlers)
        {
            try
            {
                await handler.HandleAsync(notification, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Invoke exception handlers (side-effect only)
                await _exceptionHandlerResolver
                    .InvokeNotificationExceptionHandlersAsync(notification, ex, ct)
                    .ConfigureAwait(false);

                // Immediately re-throw (remaining handlers are not invoked)
                throw;
            }
        }
    }

    /// <summary>
    /// Publishes the notification to all handlers in parallel via <c>Task.WhenAll</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All handlers execute concurrently. Exceptions are collected from faulted tasks and:
    /// </para>
    /// <list type="bullet">
    /// <item>If 1 handler throws: exception is propagated directly (not wrapped)</item>
    /// <item>If ≥2 handlers throw: exceptions are wrapped in <see cref="AggregateException"/></item>
    /// </list>
    /// <para>
    /// Exception handlers are invoked for each exception as a side-effect before propagation.
    /// </para>
    /// <para>
    /// Uses <c>.AsTask()</c> to convert <see cref="ValueTask"/> to <see cref="Task"/> for
    /// compatibility with <c>Task.WhenAll</c> (ADR-002).
    /// </para>
    /// </remarks>
    private async ValueTask PublishParallelAsync<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken ct)
        where TNotification : INotification
    {
        // Convert all ValueTask to Task for Task.WhenAll (ADR-002)
        var tasks = handlers.Select(h => h.HandleAsync(notification, ct).AsTask()).ToList();

        // Execute all tasks concurrently
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Task.WhenAll throws on first faulted task, but we need all exceptions
            // Fall through to collect from faulted tasks below
        }

        // Collect all exceptions from faulted tasks
        var exceptions = tasks
            .Where(t => t.IsFaulted && t.Exception is not null)
            .SelectMany(t => t.Exception!.InnerExceptions)
            .ToList();

        // Invoke exception handlers for each exception (side-effect only)
        if (exceptions.Count > 0)
        {
            foreach (Exception? ex in exceptions)
            {
                await _exceptionHandlerResolver
                    .InvokeNotificationExceptionHandlersAsync(notification, ex, ct)
                    .ConfigureAwait(false);
            }

            // Propagate exceptions using dual-exception pattern
            if (exceptions.Count == 1)
            {
                throw exceptions[0]; // Single exception — throw directly
            }
            else
            {
                throw new AggregateException(exceptions); // Multiple exceptions — wrap
            }
        }
    }

    /// <summary>
    /// Enqueues the notification to a bounded channel for background processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Captures the current <see cref="ExecutionContext"/> to propagate ambient context
    /// (e.g., <see cref="AsyncLocal{T}"/>) to the background service.
    /// </para>
    /// <para>
    /// The background service will:
    /// </para>
    /// <list type="number">
    /// <item>Restore the captured <see cref="ExecutionContext"/></item>
    /// <item>Create a new <c>IServiceScope</c> for each handler</item>
    /// <item>Route exceptions to <see cref="INotificationExceptionHandler{TNotification, TException}"/> instead of propagating to the caller</item>
    /// </list>
    /// </remarks>
    private async ValueTask PublishFireAndForgetAsync<TNotification>(
        TNotification notification,
        CancellationToken ct)
        where TNotification : INotification
    {
        // Capture the current ExecutionContext (for AsyncLocal propagation)
        var capturedContext = ExecutionContext.Capture();

        // Create the envelope
        var envelope = new NotificationEnvelope(
            notification,
            typeof(TNotification),
            capturedContext);

        // Enqueue to the bounded channel
        await _channelWriter.WriteAsync(envelope, ct).ConfigureAwait(false);
    }
}
