using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.TestingDemo.Domain;

namespace Nexum.Examples.TestingDemo.Commands;

public sealed class CreateProductHandler(ConcurrentDictionary<Guid, Product> store)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var product = new Product(id, command.Name, command.Price);
        store[id] = product;
        return ValueTask.FromResult(id);
    }
}
