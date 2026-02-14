using System.Collections.Concurrent;
using System.Reflection;
using Nexum.Abstractions;
using Microsoft.Extensions.Logging;

namespace Nexum.Internal;

/// <summary>
/// Resolves and invokes exception handlers using a two-axis resolution algorithm.
/// </summary>
/// <remarks>
/// <para>
/// For each message type level (concrete → base → marker interface) and each exception type level
/// (concrete → base → Exception), the resolver:
/// </para>
/// <list type="number">
/// <item>Builds the closed exception handler type (e.g., <c>ICommandExceptionHandler&lt;CreateOrderCommand, ValidationException&gt;</c>)</item>
/// <item>Resolves all registered handlers from DI via <c>IEnumerable&lt;handler_type&gt;</c></item>
/// <item>Invokes each handler's <c>HandleAsync</c> method via reflection</item>
/// <item>If a handler itself throws, logs a warning and continues (never masks the original exception)</item>
/// </list>
/// <para>
/// Resolution order example for <c>CreateOrderCommand : BaseOrderCommand : ICommand&lt;Guid&gt;</c>
/// and <c>ValidationException : NexumException : Exception</c>:
/// </para>
/// <code>
/// 1. ICommandExceptionHandler&lt;CreateOrderCommand, ValidationException&gt;
/// 2. ICommandExceptionHandler&lt;CreateOrderCommand, NexumException&gt;
/// 3. ICommandExceptionHandler&lt;CreateOrderCommand, Exception&gt;
/// 4. ICommandExceptionHandler&lt;BaseOrderCommand, ValidationException&gt;
/// 5. ICommandExceptionHandler&lt;BaseOrderCommand, NexumException&gt;
/// 6. ICommandExceptionHandler&lt;BaseOrderCommand, Exception&gt;
/// 7. ICommandExceptionHandler&lt;ICommand, Exception&gt; ← catch-all via marker
/// </code>
/// <para>
/// Exception handlers are side-effect only (logging, metrics, alerts). The original exception
/// is always re-thrown by the dispatcher after invoking all exception handlers.
/// </para>
/// <para>
/// This is a cold path (exceptions are rare in production). Reflection overhead is acceptable.
/// <c>HandleAsync</c> methods are cached in a <see cref="ConcurrentDictionary{TKey, TValue}"/> per handler type.
/// </para>
/// </remarks>
internal sealed class ExceptionHandlerResolver(
    IServiceProvider serviceProvider,
    ILogger<ExceptionHandlerResolver> logger)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider
        ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ILogger<ExceptionHandlerResolver> _logger = logger
        ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Thread-safe cache mapping handler types to their <c>HandleAsync</c> method.
    /// </summary>
    /// <remarks>
    /// Used to avoid repeated reflection lookups when invoking exception handlers.
    /// This is a cold path, but caching still improves performance on repeated exceptions of the same type.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handleAsyncMethodCache = new();

    /// <summary>
    /// Invokes all registered command exception handlers for the specified command and exception.
    /// </summary>
    /// <typeparam name="TResult">The result type of the command.</typeparam>
    /// <param name="command">The command that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Exception handlers are invoked in order from most specific to least specific (two-axis resolution).
    /// If a handler throws, the exception is logged and ignored (original exception is never masked).
    /// </remarks>
    public async ValueTask InvokeCommandExceptionHandlersAsync<TResult>(
        ICommand<TResult> command,
        Exception exception,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(exception);

        var messageTypeHierarchy = GetTypeHierarchy(command.GetType(), typeof(ICommand));
        var exceptionTypeHierarchy = GetExceptionTypeHierarchy(exception.GetType());

        await InvokeHandlersAsync(
            typeof(ICommandExceptionHandler<,>),
            messageTypeHierarchy,
            exceptionTypeHierarchy,
            command,
            exception,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes all registered query exception handlers for the specified query and exception.
    /// </summary>
    /// <typeparam name="TResult">The result type of the query.</typeparam>
    /// <param name="query">The query that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Exception handlers are invoked in order from most specific to least specific (two-axis resolution).
    /// If a handler throws, the exception is logged and ignored (original exception is never masked).
    /// </remarks>
    public async ValueTask InvokeQueryExceptionHandlersAsync<TResult>(
        IQuery<TResult> query,
        Exception exception,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(exception);

        var messageTypeHierarchy = GetTypeHierarchy(query.GetType(), typeof(IQuery));
        var exceptionTypeHierarchy = GetExceptionTypeHierarchy(exception.GetType());

        await InvokeHandlersAsync(
            typeof(IQueryExceptionHandler<,>),
            messageTypeHierarchy,
            exceptionTypeHierarchy,
            query,
            exception,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes all registered notification exception handlers for the specified notification and exception.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// Exception handlers are invoked in order from most specific to least specific (two-axis resolution).
    /// If a handler throws, the exception is logged and ignored (original exception is never masked).
    /// This is critical for <see cref="PublishStrategy.FireAndForget"/> where exceptions cannot propagate to the caller.
    /// </remarks>
    public async ValueTask InvokeNotificationExceptionHandlersAsync<TNotification>(
        TNotification notification,
        Exception exception,
        CancellationToken ct)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(exception);

        var messageTypeHierarchy = GetTypeHierarchy(notification.GetType(), typeof(INotification));
        var exceptionTypeHierarchy = GetExceptionTypeHierarchy(exception.GetType());

        await InvokeHandlersAsync(
            typeof(INotificationExceptionHandler<,>),
            messageTypeHierarchy,
            exceptionTypeHierarchy,
            notification,
            exception,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Non-generic overload for invoking notification exception handlers.
    /// Used by <see cref="NotificationBackgroundService"/> which does not have
    /// a generic <c>TNotification</c> at runtime.
    /// </summary>
    /// <param name="notification">The notification that caused the exception.</param>
    /// <param name="notificationType">The concrete notification type for handler resolution.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    public async ValueTask InvokeNotificationExceptionHandlersAsync(
        INotification notification,
        Type notificationType,
        Exception exception,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(notificationType);
        ArgumentNullException.ThrowIfNull(exception);

        var messageTypeHierarchy = GetTypeHierarchy(notificationType, typeof(INotification));
        var exceptionTypeHierarchy = GetExceptionTypeHierarchy(exception.GetType());

        await InvokeHandlersAsync(
            typeof(INotificationExceptionHandler<,>),
            messageTypeHierarchy,
            exceptionTypeHierarchy,
            notification,
            exception,
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Core algorithm: iterates over message type hierarchy and exception type hierarchy,
    /// resolving and invoking all matching exception handlers.
    /// </summary>
    /// <param name="handlerOpenGeneric">The open generic exception handler interface (e.g., <c>ICommandExceptionHandler&lt;,&gt;</c>).</param>
    /// <param name="messageTypeHierarchy">Message type hierarchy from concrete to marker interface.</param>
    /// <param name="exceptionTypeHierarchy">Exception type hierarchy from concrete to <see cref="Exception"/>.</param>
    /// <param name="message">The message instance (command/query/notification).</param>
    /// <param name="exception">The exception instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    private async ValueTask InvokeHandlersAsync(
        Type handlerOpenGeneric,
        List<Type> messageTypeHierarchy,
        List<Type> exceptionTypeHierarchy,
        object message,
        Exception exception,
        CancellationToken ct)
    {
        // Two-axis iteration: message type (outer) × exception type (inner)
        foreach (var messageType in messageTypeHierarchy)
        {
            foreach (var exceptionType in exceptionTypeHierarchy)
            {
                // Build closed handler type: e.g., ICommandExceptionHandler<CreateOrderCommand, ValidationException>
                var handlerType = handlerOpenGeneric.MakeGenericType(messageType, exceptionType);

                // Resolve all handlers of this type from DI
                var handlersEnumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
                var handlers = _serviceProvider.GetService(handlersEnumerableType) as System.Collections.IEnumerable;

                if (handlers is null)
                {
                    continue; // No handlers registered for this combination
                }

                // Invoke each handler's HandleAsync method
                foreach (var handler in handlers)
                {
                    if (handler is null)
                    {
                        continue; // Defensive: skip null entries (should not happen)
                    }

                    await InvokeHandleAsyncAsync(handler, handlerType, message, exception, ct).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Invokes the <c>HandleAsync</c> method on the specified exception handler via reflection.
    /// </summary>
    /// <param name="handler">The exception handler instance.</param>
    /// <param name="handlerType">The closed exception handler interface type.</param>
    /// <param name="message">The message instance (command/query/notification).</param>
    /// <param name="exception">The exception instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the handler throws an exception, it is logged and ignored (original exception is never masked).
    /// </remarks>
    private async ValueTask InvokeHandleAsyncAsync(
        object handler,
        Type handlerType,
        object message,
        Exception exception,
        CancellationToken ct)
    {
        try
        {
            // Get or cache the HandleAsync method for this handler type
            var handleAsyncMethod = s_handleAsyncMethodCache.GetOrAdd(
                handlerType,
                static ht =>
                {
                    // Get the HandleAsync method from the interface
                    var method = ht.GetMethod(nameof(ICommandExceptionHandler<,>.HandleAsync));
                    if (method is null)
                    {
                        throw new InvalidOperationException(
                            $"Exception handler interface '{ht.FullName}' does not have a HandleAsync method.");
                    }
                    return method;
                });

            // Invoke HandleAsync(message, exception, ct) and await the returned ValueTask
            var result = handleAsyncMethod.Invoke(handler, [message, exception, ct]);
            if (result is ValueTask valueTask)
            {
                await valueTask.ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            // Unwrap reflection exception and log the actual exception thrown by the handler
            _logger.LogWarning(
                tie.InnerException,
                "Exception handler {HandlerType} threw an exception while handling {ExceptionType}. Ignoring to preserve original exception.",
                handler.GetType().FullName,
                exception.GetType().FullName);
        }
        catch (Exception ex)
        {
            // Catch any other reflection-related exceptions (should not happen in normal operation)
            _logger.LogWarning(
                ex,
                "Failed to invoke exception handler {HandlerType} for {ExceptionType}. Ignoring to preserve original exception.",
                handler.GetType().FullName,
                exception.GetType().FullName);
        }
    }

    /// <summary>
    /// Builds the message type hierarchy from concrete to base, then appends the marker interface.
    /// </summary>
    /// <param name="messageType">The concrete message type (e.g., <c>CreateOrderCommand</c>).</param>
    /// <param name="markerInterface">The marker interface to append (e.g., <c>typeof(ICommand)</c>).</param>
    /// <returns>
    /// A list of types starting with <paramref name="messageType"/>, followed by its base classes
    /// (excluding <see cref="object"/>), ending with <paramref name="markerInterface"/>.
    /// </returns>
    /// <remarks>
    /// Example for <c>CreateOrderCommand : BaseOrderCommand : ICommand&lt;Guid&gt;</c> with marker <c>ICommand</c>:
    /// <code>
    /// [CreateOrderCommand, BaseOrderCommand, ICommand]
    /// </code>
    /// This enables fallback resolution from specific to general exception handlers.
    /// </remarks>
    private static List<Type> GetTypeHierarchy(Type messageType, Type markerInterface)
    {
        var hierarchy = new List<Type>();
        var currentType = messageType;

        // Walk up the class hierarchy (concrete → base), excluding object
        while (currentType is not null && currentType != typeof(object))
        {
            hierarchy.Add(currentType);
            currentType = currentType.BaseType;
        }

        // Append the marker interface as the final fallback
        hierarchy.Add(markerInterface);

        return hierarchy;
    }

    /// <summary>
    /// Builds the exception type hierarchy from concrete to <see cref="Exception"/> (inclusive).
    /// </summary>
    /// <param name="exceptionType">The concrete exception type (e.g., <c>ValidationException</c>).</param>
    /// <returns>
    /// A list of types starting with <paramref name="exceptionType"/>, followed by its base exception classes,
    /// ending with <see cref="Exception"/>.
    /// </returns>
    /// <remarks>
    /// Example for <c>ValidationException : NexumException : Exception</c>:
    /// <code>
    /// [ValidationException, NexumException, Exception]
    /// </code>
    /// This enables fallback resolution from specific to general exception handlers.
    /// </remarks>
    private static List<Type> GetExceptionTypeHierarchy(Type exceptionType)
    {
        var hierarchy = new List<Type>();
        var currentType = exceptionType;

        // Walk up to Exception (inclusive)
        while (currentType is not null && currentType != typeof(object))
        {
            hierarchy.Add(currentType);

            if (currentType == typeof(Exception))
            {
                break; // Stop at Exception (don't go to object)
            }

            currentType = currentType.BaseType;
        }

        return hierarchy;
    }
}
