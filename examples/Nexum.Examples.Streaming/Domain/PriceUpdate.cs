namespace Nexum.Examples.Streaming.Domain;

/// <summary>
/// Represents a single real-time price update for a stock symbol.
/// </summary>
public record PriceUpdate(string Symbol, decimal Price, DateTime Timestamp);
