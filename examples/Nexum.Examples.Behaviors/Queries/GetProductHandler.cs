using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Queries;

public sealed class GetProductHandler : IQueryHandler<GetProductQuery, decimal>
{
    public ValueTask<decimal> HandleAsync(GetProductQuery query, CancellationToken ct = default)
    {
        Console.WriteLine($"  [Handler] Fetching price for {query.ProductName}");

        decimal price = query.ProductName switch
        {
            "Widget" => 9.99m,
            "Gadget" => 24.99m,
            "Doohickey" => 4.49m,
            _ => 14.99m
        };

        return ValueTask.FromResult(price);
    }
}
