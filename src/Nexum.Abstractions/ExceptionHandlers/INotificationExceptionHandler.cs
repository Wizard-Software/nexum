namespace Nexum.Abstractions;

/// <summary>
/// Handles exceptions thrown during notification publishing.
/// Critical for <see cref="PublishStrategy.FireAndForget"/> where exceptions cannot propagate to the caller.
/// Exception handlers are side-effect only and must NOT swallow exceptions.
/// </summary>
/// <typeparam name="TNotification">The notification type. Contravariant.</typeparam>
/// <typeparam name="TException">The exception type. Contravariant.</typeparam>
public interface INotificationExceptionHandler<in TNotification, in TException>
    where TNotification : INotification
    where TException : Exception
{
    /// <summary>
    /// Handles the exception as a side-effect. The original exception is always re-thrown by the publisher.
    /// </summary>
    /// <param name="notification">The notification that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Optional cancellation token.</param>
    ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default);
}
