# Batching

`Nexum.Batching` adds automatic query batching and deduplication, similar to Facebook's DataLoader pattern. It is particularly useful for GraphQL-style resolver graphs, N+1 repository calls, and any scenario where the same query may be issued concurrently with different arguments.

## The problem it solves

Consider resolving a list of orders and their customers in a GraphQL-like flow:

```csharp
foreach (var order in orders)
{
    var customer = await dispatcher.DispatchAsync(new GetCustomerQuery(order.CustomerId), ct);
    // ... use customer ...
}
```

Without batching this is a textbook N+1 problem: N sequential round-trips. With batching, Nexum can:

1. Collect all `GetCustomerQuery` calls within a short time window.
2. Deduplicate by key (`CustomerId`).
3. Issue a single `GetCustomersBatchQuery` to the batch handler.
4. Return each caller's result from the batch response.

## Defining a batch query

```csharp
public record GetCustomerQuery(Guid CustomerId)
    : IBatchQuery<Guid, CustomerDto?>
{
    public Guid BatchKey => CustomerId;
}
```

The `IBatchQuery<TKey, TResult>` interface tells Nexum which key to group on. Queries with the same batch key within a single batching window collapse into one call to the batch handler.

## Batch handler

```csharp
public sealed class CustomerBatchHandler : IBatchQueryHandler<GetCustomerQuery, Guid, CustomerDto?>
{
    private readonly ICustomerRepository _repo;
    public CustomerBatchHandler(ICustomerRepository repo) => _repo = repo;

    public async ValueTask<IReadOnlyDictionary<Guid, CustomerDto?>> HandleBatchAsync(
        IReadOnlyList<GetCustomerQuery> batch,
        CancellationToken ct)
    {
        var ids = batch.Select(q => q.CustomerId).Distinct().ToArray();
        var customers = await _repo.GetManyAsync(ids, ct);
        return customers.ToDictionary(c => c.Id, c => (CustomerDto?)c.ToDto());
    }
}
```

## Configuration

```csharp
services.AddNexum();
services.AddNexumBatching(options =>
{
    options.BatchWindow = TimeSpan.FromMilliseconds(5);
    options.MaxBatchSize = 200;
});
```

`BatchWindow` is the maximum time a query waits for siblings to join the same batch. Shorter windows reduce latency; longer windows produce bigger batches. `MaxBatchSize` caps the batch even if the window is still open.

## Dispatch remains unchanged

Consumers do not know batching is happening:

```csharp
var customer = await dispatcher.DispatchAsync(new GetCustomerQuery(id), ct);
```

The dispatcher transparently routes through the batching pipeline when a batch handler is registered for the query type.
