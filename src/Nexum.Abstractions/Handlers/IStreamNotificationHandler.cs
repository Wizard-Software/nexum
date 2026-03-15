namespace Nexum.Abstractions;

/// <summary>
/// Defines a handler for streaming notifications of type <typeparamref name="TNotification"/>
/// that produce an asynchronous sequence of <typeparamref name="TItem"/>.
/// </summary>
/// <typeparam name="TNotification">The streaming notification type to handle. Contravariant.</typeparam>
/// <typeparam name="TItem">The type of each element in the notification stream.</typeparam>
public interface IStreamNotificationHandler<in TNotification, TItem>
    where TNotification : IStreamNotification<TItem>
{
    /// <summary>
    /// Handles the specified streaming notification, returning an asynchronous sequence of items.
    /// </summary>
    /// <param name="notification">The streaming notification to handle.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> representing the item stream.</returns>
    IAsyncEnumerable<TItem> HandleAsync(TNotification notification, CancellationToken ct = default);
}
