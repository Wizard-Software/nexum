using System.Collections.Concurrent;
using Nexum.Examples.MigrationFromMediatR.Domain;

namespace Nexum.Examples.MigrationFromMediatR.Shared;

// In-memory store — registered as singleton so both MediatR and Nexum handlers share the same data.
// In a real migration you would use your actual database/repository here.
public sealed class CustomerStore
{
    private readonly ConcurrentDictionary<Guid, Customer> _customers = new();

    public Guid Add(string name, string email)
    {
        var id = Guid.NewGuid();
        var customer = new Customer(id, name, email);
        _customers[id] = customer;
        return id;
    }

    public Customer? GetById(Guid id) =>
        _customers.TryGetValue(id, out var customer) ? customer : null;

    public bool Remove(Guid id) => _customers.TryRemove(id, out _);
}
