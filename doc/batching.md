# Batching

The `Nexum.Batching` package provides automatic query batching and deduplication. Multiple concurrent queries of the same type are collected into a single batch and executed together, reducing database round-trips.

## Installation

```bash
dotnet add package Nexum.Batching
```

## When to Use

Batching is useful when:
- Many concurrent requests query the same type of data (e.g., user profiles, product details)
- You want to reduce N+1 query patterns without restructuring calling code
- Your data source supports batch lookups efficiently (e.g., `WHERE id IN (...)`)

## IBatchQueryHandler

Instead of handling one query at a time, implement `IBatchQueryHandler<TQuery, TKey, TResult>` to process a batch:

```csharp
public record GetUserQuery(Guid UserId) : IQuery<UserDto?>;

public class GetUserBatchHandler : IBatchQueryHandler<GetUserQuery, Guid, UserDto?>
{
    private readonly IUserRepository _repo;

    public GetUserBatchHandler(IUserRepository repo) => _repo = repo;

    public Guid GetKey(GetUserQuery query) => query.UserId;

    public async ValueTask<IReadOnlyDictionary<Guid, UserDto?>> HandleAsync(
        IReadOnlyList<GetUserQuery> queries, CancellationToken ct)
    {
        var userIds = queries.Select(q => q.UserId).Distinct().ToList();
        var users = await _repo.GetByIdsAsync(userIds, ct);

        return userIds.ToDictionary(
            id => id,
            id => users.FirstOrDefault(u => u.Id == id));
    }
}
```

The interface:

```csharp
public interface IBatchQueryHandler<in TQuery, TKey, TResult>
    where TQuery : IQuery<TResult>
    where TKey : notnull
{
    TKey GetKey(TQuery query);
    ValueTask<IReadOnlyDictionary<TKey, TResult>> HandleAsync(
        IReadOnlyList<TQuery> queries, CancellationToken ct = default);
}
```

- `GetKey` extracts a deduplication key from each query. Queries with the same key are deduplicated within a batch.
- `HandleAsync` receives all collected queries and returns a dictionary mapping keys to results.

## How It Works

1. Multiple callers dispatch `GetUserQuery` concurrently.
2. The `BatchingQueryDispatcher` collects them in a buffer.
3. After the **batch window** expires or the **max batch size** is reached, the buffer is flushed.
4. All collected queries are sent to `IBatchQueryHandler.HandleAsync` as a single batch.
5. Each caller receives their individual result from the returned dictionary.

```
Caller A: DispatchAsync(GetUserQuery(id=1)) ─┐
Caller B: DispatchAsync(GetUserQuery(id=2)) ─┤  batch window (10ms)
Caller C: DispatchAsync(GetUserQuery(id=1)) ─┘       │
                                                       ▼
                                              HandleAsync([id=1, id=2])
                                                       │
                                              {1: UserA, 2: UserB}
                                                       │
                                    ┌──────────────────┼──────────────┐
                                    ▼                  ▼              ▼
                              Caller A: UserA    Caller B: UserB    Caller C: UserA
```

Note: Caller A and Caller C both queried `id=1` -- the query was deduplicated.

## Configuration

### NexumBatchingOptions

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `BatchWindow` | `TimeSpan` | `10ms` | 1ms -- 30s | Time to collect queries before flushing |
| `MaxBatchSize` | `int` | `100` | 1 -- 10,000 | Max queries per batch (flushes early if reached) |
| `DrainTimeout` | `TimeSpan` | `5s` | -- | Grace period on shutdown for in-flight batches |

```csharp
builder.Services.AddNexumBatching(options =>
{
    options.BatchWindow = TimeSpan.FromMilliseconds(20);
    options.MaxBatchSize = 200;
    options.DrainTimeout = TimeSpan.FromSeconds(10);
});
```

## Mixing Batch and Regular Handlers

Batching only activates for query types that have a registered `IBatchQueryHandler`. All other queries flow through the standard `IQueryHandler` pipeline as usual.

If both an `IBatchQueryHandler` and an `IQueryHandler` are registered for the same query type, the batch handler takes precedence when dispatched through the `BatchingQueryDispatcher`.
