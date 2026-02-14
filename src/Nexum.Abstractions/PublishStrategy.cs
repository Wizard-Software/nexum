namespace Nexum.Abstractions;

/// <summary>
/// Defines the strategy used to publish notifications to their handlers.
/// </summary>
public enum PublishStrategy
{
    /// <summary>
    /// Handlers are executed one after another. All handlers execute regardless of exceptions.
    /// If one handler throws, the exception is propagated directly.
    /// If multiple handlers throw, exceptions are wrapped in <see cref="AggregateException"/>.
    /// </summary>
    Sequential,

    /// <summary>
    /// All handlers are executed concurrently via <c>Task.WhenAll</c>.
    /// If one handler throws, the exception is propagated directly.
    /// If multiple handlers throw, exceptions are wrapped in <see cref="AggregateException"/>.
    /// </summary>
    Parallel,

    /// <summary>
    /// Handlers are executed sequentially. The first exception immediately stops execution
    /// and is propagated to the caller; remaining handlers are not invoked.
    /// </summary>
    StopOnException,

    /// <summary>
    /// The notification is published in the background via a bounded channel and a
    /// <c>BackgroundService</c>. Each handler runs in a new <c>IServiceScope</c>.
    /// Exceptions are routed to <c>INotificationExceptionHandler</c> instead of propagating to the caller.
    /// </summary>
    FireAndForget
}
