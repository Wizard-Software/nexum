using Microsoft.AspNetCore.SignalR;
using Nexum.Abstractions;

namespace Nexum.Streaming;

/// <summary>
/// Abstract base class for SignalR hubs that integrate with Nexum streaming.
/// Provides protected helper methods for dispatching stream queries and stream notifications.
/// </summary>
/// <remarks>
/// <para>
/// This is the runtime path (Path 1) of the dual-path SignalR integration.
/// Users inherit from this class and create concrete hub methods that call the protected helpers.
/// For zero-boilerplate, use <see cref="NexumStreamHubAttribute"/> with Source Generator (Path 2).
/// </para>
/// <para>
/// <b>Design note:</b> This class is intentionally not sealed (exception from Z8 convention).
/// SignalR Hub methods must be public non-generic, so users must create concrete subclasses
/// with typed hub methods. See ADR-011 for rationale.
/// </para>
/// <example>
/// Runtime path (manual concrete hub method):
/// <code>
/// public sealed class OrderStreamHub : NexumStreamHubBase
/// {
///     public OrderStreamHub(IQueryDispatcher qd, IStreamNotificationPublisher snp) : base(qd, snp) { }
///
///     public IAsyncEnumerable&lt;OrderUpdate&gt; StreamOrders(
///         GetOrderUpdatesQuery query, CancellationToken ct)
///         => StreamQueryAsync&lt;GetOrderUpdatesQuery, OrderUpdate&gt;(query, ct);
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class NexumStreamHubBase : Hub
{
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly IStreamNotificationPublisher _notificationPublisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="NexumStreamHubBase"/> class.
    /// </summary>
    /// <param name="queryDispatcher">The query dispatcher used to execute stream queries.</param>
    /// <param name="notificationPublisher">The publisher used to stream notifications to handlers.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="queryDispatcher"/> or <paramref name="notificationPublisher"/> is <see langword="null"/>.
    /// </exception>
    protected NexumStreamHubBase(
        IQueryDispatcher queryDispatcher,
        IStreamNotificationPublisher notificationPublisher)
    {
        ArgumentNullException.ThrowIfNull(queryDispatcher);
        ArgumentNullException.ThrowIfNull(notificationPublisher);
        _queryDispatcher = queryDispatcher;
        _notificationPublisher = notificationPublisher;
    }

    /// <summary>
    /// Dispatches a stream query and returns the result stream.
    /// Call this from a concrete public hub method.
    /// </summary>
    /// <typeparam name="TQuery">The stream query type.</typeparam>
    /// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
    /// <param name="query">The stream query to dispatch.</param>
    /// <param name="ct">A cancellation token to cancel the stream.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> representing the result stream.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
    protected IAsyncEnumerable<TResult> StreamQueryAsync<TQuery, TResult>(
        TQuery query, CancellationToken ct)
        where TQuery : IStreamQuery<TResult>
    {
        ArgumentNullException.ThrowIfNull(query);
        return _queryDispatcher.StreamAsync<TResult>(query, ct);
    }

    /// <summary>
    /// Publishes a stream notification and returns the merged item stream from all registered handlers.
    /// Call this from a concrete public hub method.
    /// </summary>
    /// <typeparam name="TNotification">The stream notification type.</typeparam>
    /// <typeparam name="TItem">The type of each item in the merged stream.</typeparam>
    /// <param name="notification">The stream notification to publish.</param>
    /// <param name="ct">A cancellation token to cancel the stream.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TItem}"/> representing the merged item stream from all handlers.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="notification"/> is <see langword="null"/>.</exception>
    protected IAsyncEnumerable<TItem> StreamNotificationAsync<TNotification, TItem>(
        TNotification notification, CancellationToken ct)
        where TNotification : IStreamNotification<TItem>
    {
        ArgumentNullException.ThrowIfNull(notification);
        return _notificationPublisher.StreamAsync<TNotification, TItem>(notification, ct);
    }
}
