using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.WebApi.Domain;

namespace Nexum.Examples.WebApi.Commands;

// Handles CreateOrderCommand: stores the new order in the shared in-memory dictionary
// and returns the generated Guid. The ConcurrentDictionary<Guid, Order> singleton is
// shared with GetOrderHandler to simulate a simple in-memory data store.
public sealed class CreateOrderHandler(ConcurrentDictionary<Guid, Order> store)
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var total = command.Quantity * command.UnitPrice;
        var order = new Order(id, command.Product, command.Quantity, total);

        store[id] = order;
        return ValueTask.FromResult(id);
    }
}
