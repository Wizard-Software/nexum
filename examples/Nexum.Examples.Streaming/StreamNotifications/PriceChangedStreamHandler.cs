using System.Runtime.CompilerServices;
using Nexum.Abstractions;
using Nexum.Examples.Streaming.Domain;

namespace Nexum.Examples.Streaming.StreamNotifications;

/// <summary>
/// Handles <see cref="PriceChangedNotification"/> by streaming price updates for the notified symbol.
///
/// Streaming notification flow:
///   Hub method calls StreamNotificationAsync → IStreamNotificationPublisher resolves all
///   registered IStreamNotificationHandler implementations → merges their streams via channels
///   → SignalR client receives merged IAsyncEnumerable of PriceUpdate items
/// </summary>
public sealed class PriceChangedStreamHandler
    : IStreamNotificationHandler<PriceChangedNotification, PriceUpdate>
{
    private static readonly Random s_random = new();

    public async IAsyncEnumerable<PriceUpdate> HandleAsync(
        PriceChangedNotification notification,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Stream 10 price updates for the notified symbol, then stop
        for (int i = 0; i < 10 && !ct.IsCancellationRequested; i++)
        {
            decimal basePrice = 100m;
            decimal fluctuation = (decimal)(s_random.NextDouble() * 4.0 - 2.0);
            decimal price = Math.Round(basePrice + fluctuation, 2);

            yield return new PriceUpdate(notification.Symbol, price, DateTime.UtcNow);

            await Task.Delay(500, ct).ConfigureAwait(false);
        }
    }
}
