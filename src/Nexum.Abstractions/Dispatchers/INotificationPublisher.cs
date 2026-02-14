namespace Nexum.Abstractions;

/// <summary>
/// Publishes notifications to all registered handlers using the specified strategy.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes the specified notification to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to publish.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="strategy">
    /// The publish strategy. When <c>null</c>, uses <c>NexumOptions.DefaultPublishStrategy</c>.
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
    ValueTask PublishAsync<TNotification>(
        TNotification notification,
        PublishStrategy? strategy = null,
        CancellationToken ct = default) where TNotification : INotification;
}
