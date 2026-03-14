using System.Collections.Concurrent;
using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// An in-memory implementation of <see cref="INotificationPublisher"/> that collects published
/// notifications for later verification in tests. Does not delegate to any real handlers.
/// </summary>
/// <remarks>
/// This class is thread-safe. <see cref="PublishedNotifications"/> and <see cref="GetPublished{TNotification}"/>
/// return point-in-time snapshots; concurrent publishes after the snapshot is taken are not reflected.
/// </remarks>
public sealed class InMemoryNotificationCollector : INotificationPublisher
{
    private ConcurrentQueue<object> _notifications = new();

    /// <summary>
    /// Collects the notification without dispatching it to any handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <param name="notification">The notification to collect.</param>
    /// <param name="strategy">Ignored. Included for interface compatibility.</param>
    /// <param name="ct">Ignored. Included for interface compatibility.</param>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask PublishAsync<TNotification>(
        TNotification notification,
        PublishStrategy? strategy = null,
        CancellationToken ct = default) where TNotification : INotification
    {
        _notifications.Enqueue(notification);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Returns a snapshot of all notifications published so far, in insertion order.
    /// </summary>
    public IReadOnlyList<object> PublishedNotifications => _notifications.ToArray();

    /// <summary>
    /// Returns a snapshot of all published notifications of the specified type, in insertion order.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to filter by.</typeparam>
    /// <returns>A list of matching notifications.</returns>
    public IReadOnlyList<TNotification> GetPublished<TNotification>() where TNotification : INotification
        => _notifications.OfType<TNotification>().ToList();

    /// <summary>
    /// Clears all collected notifications.
    /// </summary>
    public void Reset()
    {
        _notifications = new ConcurrentQueue<object>();
    }
}
