using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Behaviors;

[BehaviorOrder(1)]
public sealed class FilteringStreamBehavior<TQuery, TResult> : IStreamQueryBehavior<TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    public async IAsyncEnumerable<TResult> HandleAsync(
        TQuery query, StreamQueryHandlerDelegate<TResult> next,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Console.WriteLine("  [FILTER] Stream behavior active");
        await foreach (var item in next(ct).WithCancellation(ct).ConfigureAwait(false))
        {
            Console.WriteLine($"  [FILTER] Passing through: {item}");
            yield return item;
        }
    }
}
