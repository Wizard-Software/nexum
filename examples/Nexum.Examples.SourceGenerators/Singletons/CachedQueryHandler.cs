using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Singletons;

/// <summary>Query that returns a cached exchange rate for demonstration purposes.</summary>
public sealed record GetExchangeRateQuery(string Currency) : IQuery<decimal>;

// SG Tier 1: Discovered via [QueryHandler] — registered as Singleton in NexumHandlerRegistry.AddNexumHandlers().
//            Without SG, assembly scanning also reads [HandlerLifetime] via reflection.
//
// [HandlerLifetime(NexumLifetime.Singleton)] — SG registers this handler as Singleton
//            instead of the default Scoped. Useful for handlers that cache data in memory
//            (e.g., exchange rates, configuration lookups) and are safe to share across scopes.
[HandlerLifetime(NexumLifetime.Singleton)]
[QueryHandler]
public sealed class CachedQueryHandler : IQueryHandler<GetExchangeRateQuery, decimal>
{
    private readonly Dictionary<string, decimal> _cache = new(StringComparer.OrdinalIgnoreCase)
    {
        ["USD"] = 1.00m,
        ["EUR"] = 0.92m,
        ["GBP"] = 0.79m,
    };

    // This instance is shared for the entire application lifetime (Singleton).
    // The same CachedQueryHandler instance serves all scopes and requests.
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<decimal> HandleAsync(GetExchangeRateQuery query, CancellationToken ct = default)
    {
        var rate = _cache.GetValueOrDefault(query.Currency, -1m);
        Console.WriteLine($"  [CachedQueryHandler] Instance={_instanceId:N} rate({query.Currency})={rate}");
        return ValueTask.FromResult(rate);
    }
}
