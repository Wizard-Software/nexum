using Nexum.Batching;
using Nexum.Examples.Batching.Data;
using Nexum.Examples.Batching.Domain;

namespace Nexum.Examples.Batching.Queries;

/// <summary>
/// Batch handler for <see cref="GetProductByIdQuery"/>.
/// Demonstrates how Nexum batching collapses N concurrent queries
/// into a single handler invocation — preventing N+1 database calls.
/// </summary>
/// <remarks>
/// The batching layer accumulates queries arriving within the configured
/// BatchWindow (50 ms in this example) and delivers them here as one list.
/// </remarks>
public sealed class GetProductByIdBatchHandler
    : IBatchQueryHandler<GetProductByIdQuery, int, Product>
{
    /// <summary>
    /// Extracts the product ID key used for deduplication and result mapping.
    /// </summary>
    public int GetKey(GetProductByIdQuery query) => query.ProductId;

    /// <summary>
    /// Resolves all queried products in a single batch operation.
    /// In a real application this would be a single SQL query with an IN clause.
    /// </summary>
    public ValueTask<IReadOnlyDictionary<int, Product>> HandleAsync(
        IReadOnlyList<GetProductByIdQuery> queries,
        CancellationToken ct = default)
    {
        // Demonstrate that all queries arrive together as one batch
        Console.WriteLine($"  [Batch] Received {queries.Count} queries in a single batch!");

        // Resolve all products from the store in one pass (simulates a bulk DB fetch)
        var results = queries
            .Select(q => ProductStore.All.TryGetValue(q.ProductId, out var product)
                ? product
                : null)
            .Where(p => p is not null)
            .ToDictionary(p => p!.Id, p => p!);

        return ValueTask.FromResult<IReadOnlyDictionary<int, Product>>(results);
    }
}
