using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Queries;

public sealed class ListProductsStreamHandler : IStreamQueryHandler<ListProductsStreamQuery, string>
{
    private static readonly (string Name, decimal Price)[] s_products =
    [
        ("Widget", 9.99m),
        ("Gadget", 24.99m),
        ("Doohickey", 4.49m),
        ("Thingamajig", 14.99m),
        ("Whatsit", 2.99m)
    ];

    public async IAsyncEnumerable<string> HandleAsync(
        ListProductsStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var (name, price) in s_products)
        {
            ct.ThrowIfCancellationRequested();

            if (price >= query.MinPrice)
            {
                Console.WriteLine($"  [Handler] Yielding product: {name} (${price:F2})");
                await Task.Yield();
                yield return name;
            }
        }
    }
}
