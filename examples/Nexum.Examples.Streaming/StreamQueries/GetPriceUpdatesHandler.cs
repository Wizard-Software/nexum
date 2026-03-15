using System.Runtime.CompilerServices;
using Nexum.Abstractions;
using Nexum.Examples.Streaming.Domain;

namespace Nexum.Examples.Streaming.StreamQueries;

/// <summary>
/// Handles <see cref="GetPriceUpdatesQuery"/> by generating simulated price updates
/// in an infinite loop until the cancellation token is triggered (e.g., client disconnects).
///
/// Streaming flow:
///   Client connects → SSE endpoint creates CancellationToken tied to HTTP connection
///   → IQueryDispatcher.StreamAsync() resolves this handler
///   → HandleAsync() loops, yielding one PriceUpdate per second
///   → Client disconnects → CancellationToken cancelled → loop exits cleanly
/// </summary>
public sealed class GetPriceUpdatesHandler : IStreamQueryHandler<GetPriceUpdatesQuery, PriceUpdate>
{
    private static readonly string[] s_symbols = ["AAPL", "MSFT", "GOOG", "AMZN", "NVDA"];

    public async IAsyncEnumerable<PriceUpdate> HandleAsync(
        GetPriceUpdatesQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var random = new Random();

        // Determine which symbols to stream based on optional query filter
        string[] activeSymbols = query.Symbol is { Length: > 0 }
            ? [query.Symbol]
            : s_symbols;

        // Loop until the client disconnects (CancellationToken cancelled)
        while (!ct.IsCancellationRequested)
        {
            string symbol = activeSymbols[random.Next(activeSymbols.Length)];
            decimal basePrice = symbol switch
            {
                "AAPL" => 175m,
                "MSFT" => 420m,
                "GOOG" => 170m,
                "AMZN" => 185m,
                "NVDA" => 850m,
                _ => 100m
            };

            // Simulate realistic price fluctuation ±2%
            decimal fluctuation = (decimal)(random.NextDouble() * 4.0 - 2.0);
            decimal price = Math.Round(basePrice + fluctuation, 2);

            yield return new PriceUpdate(symbol, price, DateTime.UtcNow);

            // Emit one update per second; Task.Delay respects cancellation
            await Task.Delay(1000, ct).ConfigureAwait(false);
        }
    }
}
