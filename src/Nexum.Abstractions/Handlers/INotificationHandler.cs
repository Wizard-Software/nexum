namespace Nexum.Abstractions;

/// <summary>
/// Defines a handler for notifications of type <typeparamref name="TNotification"/>.
/// Notification handlers do not return a result.
/// </summary>
/// <typeparam name="TNotification">The notification type to handle. Contravariant.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>
    /// Handles the specified notification asynchronously.
    /// </summary>
    /// <param name="notification">The notification to handle.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    ValueTask HandleAsync(TNotification notification, CancellationToken ct = default);
}
