using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.WebApi.Domain;

namespace Nexum.Examples.WebApi.Queries;

// Handles GetOrderQuery: looks up the order from the shared in-memory dictionary.
// Returns null when no order with the requested Guid exists.
public sealed class GetOrderHandler(ConcurrentDictionary<Guid, Order> store)
    : IQueryHandler<GetOrderQuery, Order?>
{
    public ValueTask<Order?> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
    {
        store.TryGetValue(query.Id, out var order);
        return ValueTask.FromResult(order);
    }
}
