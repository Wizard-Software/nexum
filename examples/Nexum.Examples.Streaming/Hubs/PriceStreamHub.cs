using Nexum.Abstractions;
using Nexum.Examples.Streaming.Domain;
using Nexum.Examples.Streaming.StreamNotifications;
using Nexum.Examples.Streaming.StreamQueries;
using Nexum.Streaming;

namespace Nexum.Examples.Streaming.Hubs;

/// <summary>
/// SignalR hub for streaming price updates to connected clients.
///
/// Runtime path (Path 1): concrete hub methods delegate to protected helpers in NexumStreamHubBase.
/// SignalR Hub methods MUST be public and non-generic (dotnet/aspnetcore#6949).
/// See ADR-011 for the dual-path design rationale.
/// </summary>
public sealed class PriceStreamHub(
    IQueryDispatcher queryDispatcher,
    IStreamNotificationPublisher notificationPublisher)
    : NexumStreamHubBase(queryDispatcher, notificationPublisher)
{
    /// <summary>
    /// Streams real-time price updates via a stream query.
    /// Client calls: connection.stream("StreamPrices", { symbol: "AAPL" })
    /// </summary>
    public IAsyncEnumerable<PriceUpdate> StreamPrices(
        GetPriceUpdatesQuery query, CancellationToken ct)
        => StreamQueryAsync<GetPriceUpdatesQuery, PriceUpdate>(query, ct);

    /// <summary>
    /// Streams price change notifications via stream notification publisher.
    /// Multiple handlers' streams are merged into a single IAsyncEnumerable.
    /// </summary>
    public IAsyncEnumerable<PriceUpdate> StreamPriceNotifications(
        PriceChangedNotification notification, CancellationToken ct)
        => StreamNotificationAsync<PriceChangedNotification, PriceUpdate>(notification, ct);
}
