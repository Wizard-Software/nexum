using Nexum.Abstractions;

namespace Nexum.Batching.Tests.Fixtures;

public record GetItemByIdQuery(Guid ItemId) : IQuery<string>;

public record GetItemByNameQuery(string Name) : IQuery<int>;

public sealed class GetItemByIdBatchHandler : IBatchQueryHandler<GetItemByIdQuery, Guid, string>
{
    public Guid GetKey(GetItemByIdQuery query) => query.ItemId;

    public ValueTask<IReadOnlyDictionary<Guid, string>> HandleAsync(
        IReadOnlyList<GetItemByIdQuery> queries, CancellationToken ct = default)
    {
        var results = queries.ToDictionary(
            q => q.ItemId,
            q => $"Item-{q.ItemId}");
        return ValueTask.FromResult<IReadOnlyDictionary<Guid, string>>(results);
    }
}

// A handler that simulates missing keys
public sealed class IncompleteResultBatchHandler : IBatchQueryHandler<GetItemByIdQuery, Guid, string>
{
    public Guid GetKey(GetItemByIdQuery query) => query.ItemId;

    public ValueTask<IReadOnlyDictionary<Guid, string>> HandleAsync(
        IReadOnlyList<GetItemByIdQuery> queries, CancellationToken ct = default)
    {
        // Only return results for the first query, missing the rest
        var results = new Dictionary<Guid, string>();
        if (queries.Count > 0)
        {
            results[queries[0].ItemId] = $"Item-{queries[0].ItemId}";
        }
        return ValueTask.FromResult<IReadOnlyDictionary<Guid, string>>(results);
    }
}

// A handler that throws
public sealed class ThrowingBatchHandler : IBatchQueryHandler<GetItemByIdQuery, Guid, string>
{
    public Guid GetKey(GetItemByIdQuery query) => query.ItemId;

    public ValueTask<IReadOnlyDictionary<Guid, string>> HandleAsync(
        IReadOnlyList<GetItemByIdQuery> queries, CancellationToken ct = default)
    {
        throw new InvalidOperationException("Batch handler failed");
    }
}

// Query handler for non-batched pass-through testing
public sealed class GetItemByNameQueryHandler : IQueryHandler<GetItemByNameQuery, int>
{
    public ValueTask<int> HandleAsync(GetItemByNameQuery query, CancellationToken ct = default)
    {
        return ValueTask.FromResult(query.Name.Length);
    }
}

// Stream query for pass-through testing
public record StreamItemsQuery : IStreamQuery<string>;

public sealed class StreamItemsQueryHandler : IStreamQueryHandler<StreamItemsQuery, string>
{
    public async IAsyncEnumerable<string> HandleAsync(
        StreamItemsQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "a";
        yield return "b";
        await Task.CompletedTask;
    }
}
