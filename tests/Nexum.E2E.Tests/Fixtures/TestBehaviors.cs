using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

// Logging behavior — tracks execution order via shared list
public sealed class TrackingCommandBehavior(List<string> executionLog)
    : ICommandBehavior<TrackedCommand, string>
{
    public async ValueTask<string> HandleAsync(
        TrackedCommand command,
        CommandHandlerDelegate<string> next,
        CancellationToken ct = default)
    {
        executionLog.Add("Behavior:Before");
        var result = await next(ct).ConfigureAwait(false);
        executionLog.Add("Behavior:After");
        return result;
    }
}

public sealed class OuterTrackingBehavior(List<string> executionLog)
    : ICommandBehavior<TrackedCommand, string>
{
    public async ValueTask<string> HandleAsync(
        TrackedCommand command,
        CommandHandlerDelegate<string> next,
        CancellationToken ct = default)
    {
        executionLog.Add("Outer:Before");
        var result = await next(ct).ConfigureAwait(false);
        executionLog.Add("Outer:After");
        return result;
    }
}

public sealed class InnerTrackingBehavior(List<string> executionLog)
    : ICommandBehavior<TrackedCommand, string>
{
    public async ValueTask<string> HandleAsync(
        TrackedCommand command,
        CommandHandlerDelegate<string> next,
        CancellationToken ct = default)
    {
        executionLog.Add("Inner:Before");
        var result = await next(ct).ConfigureAwait(false);
        executionLog.Add("Inner:After");
        return result;
    }
}

// Caching query behavior — caches by query key
public sealed class CachingQueryBehavior(ConcurrentDictionary<string, object> cache)
    : IQueryBehavior<GetProductPriceQuery, decimal>
{
    public async ValueTask<decimal> HandleAsync(
        GetProductPriceQuery query,
        QueryHandlerDelegate<decimal> next,
        CancellationToken ct = default)
    {
        var key = $"price:{query.ProductName}";
        if (cache.TryGetValue(key, out var cached))
        {
            return (decimal)cached;
        }

        var result = await next(ct).ConfigureAwait(false);
        cache[key] = result;
        return result;
    }
}

// Filtering stream behavior — filters items based on query min price
public sealed class FilteringStreamBehavior
    : IStreamQueryBehavior<ListPricesStreamQuery, decimal>
{
    public async IAsyncEnumerable<decimal> HandleAsync(
        ListPricesStreamQuery query,
        StreamQueryHandlerDelegate<decimal> next,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var price in next(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            if (price >= query.MinPrice)
            {
                yield return price;
            }
        }
    }
}
