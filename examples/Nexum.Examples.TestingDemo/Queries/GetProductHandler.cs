using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.TestingDemo.Domain;

namespace Nexum.Examples.TestingDemo.Queries;

public sealed class GetProductHandler(ConcurrentDictionary<Guid, Product> store)
    : IQueryHandler<GetProductQuery, Product?>
{
    public ValueTask<Product?> HandleAsync(GetProductQuery query, CancellationToken ct = default)
    {
        store.TryGetValue(query.Id, out var product);
        return ValueTask.FromResult(product);
    }
}
