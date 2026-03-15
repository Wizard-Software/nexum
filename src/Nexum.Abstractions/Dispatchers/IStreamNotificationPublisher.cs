namespace Nexum.Abstractions;

/// <summary>
/// Publishes streaming notifications to all registered handlers, merging their output streams.
/// </summary>
public interface IStreamNotificationPublisher
{
    /// <summary>
    /// Publishes the specified streaming notification and returns a merged stream of items
    /// from all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of streaming notification.</typeparam>
    /// <typeparam name="TItem">The type of each element in the merged stream.</typeparam>
    /// <param name="notification">The streaming notification to publish.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> representing the merged item stream from all handlers.</returns>
    IAsyncEnumerable<TItem> StreamAsync<TNotification, TItem>(
        TNotification notification, CancellationToken ct = default)
        where TNotification : IStreamNotification<TItem>;
}
