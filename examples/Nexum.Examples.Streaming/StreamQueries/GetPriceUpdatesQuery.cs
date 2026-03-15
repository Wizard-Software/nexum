using Nexum.Abstractions;
using Nexum.Examples.Streaming.Domain;

namespace Nexum.Examples.Streaming.StreamQueries;

/// <summary>
/// Stream query that requests a continuous feed of simulated price updates.
/// Bound from query string parameters (e.g., ?Symbol=AAPL) by the SSE endpoint.
/// </summary>
public record GetPriceUpdatesQuery : IStreamQuery<PriceUpdate>
{
    /// <summary>
    /// Optional symbol filter. When null or empty, all symbols are streamed.
    /// </summary>
    public string? Symbol { get; init; }
}
