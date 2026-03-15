using System.Collections.Concurrent;
using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Behaviors;

[BehaviorOrder(1)]
public sealed class CachingQueryBehavior<TQuery, TResult> : IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private static readonly ConcurrentDictionary<string, object?> s_cache = new();

    public async ValueTask<TResult> HandleAsync(
        TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        var key = typeof(TQuery).Name + ":" + query.ToString();
        if (s_cache.TryGetValue(key, out var cached))
        {
            Console.WriteLine($"  [CACHE HIT] {key}");
            return (TResult)cached!;
        }

        var result = await next(ct).ConfigureAwait(false);
        s_cache.TryAdd(key, result);
        Console.WriteLine($"  [CACHE MISS] {key} — stored");
        return result;
    }
}
