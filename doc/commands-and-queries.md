# Commands and Queries

Nexum enforces strict CQRS -- commands and queries are separate types with dedicated dispatchers and pipelines.

## Commands

A command represents an intent to **modify state**. Define commands by implementing `ICommand<TResult>`.

### Defining Commands

```csharp
using Nexum.Abstractions;

// Command with a return value
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items) : ICommand<Guid>;

// Void command (no return value)
public record DeleteOrderCommand(Guid OrderId) : IVoidCommand;
```

The interface hierarchy:
- `ICommand` -- non-generic marker interface for all commands
- `ICommand<TResult> : ICommand` -- generic command with typed result
- `IVoidCommand : ICommand<Unit>` -- convenience alias for commands returning nothing

Use `record` or `record struct` for immutable, value-equality-based command types.

### Command Handlers

Implement `ICommandHandler<TCommand, TResult>` to handle a command:

```csharp
[CommandHandler]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;

    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<Guid> HandleAsync(
        CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        await _repo.SaveAsync(order, ct);
        return order.Id;
    }
}
```

For void commands:

```csharp
[CommandHandler]
public class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand, Unit>
{
    private readonly IOrderRepository _repo;

    public DeleteOrderHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<Unit> HandleAsync(
        DeleteOrderCommand command, CancellationToken ct)
    {
        await _repo.DeleteAsync(command.OrderId, ct);
        return Unit.Value;
    }
}
```

### Dispatching Commands

Inject `ICommandDispatcher` and call `DispatchAsync`:

```csharp
public class OrderService(ICommandDispatcher commandDispatcher)
{
    public async Task<Guid> CreateOrderAsync(string customerId, List<OrderItemDto> items, CancellationToken ct)
    {
        var command = new CreateOrderCommand(customerId, items);
        return await commandDispatcher.DispatchAsync(command, ct);
    }
}
```

Signature:

```csharp
ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
```

## Queries

A query represents an intent to **read state** without side effects. Define queries by implementing `IQuery<TResult>`.

### Defining Queries

```csharp
using Nexum.Abstractions;

public record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>;

public record GetOrdersQuery(string CustomerId, int Page, int PageSize) : IQuery<IReadOnlyList<OrderDto>>;
```

The interface hierarchy:
- `IQuery` -- non-generic marker interface for all queries
- `IQuery<TResult> : IQuery` -- generic query with typed result

### Query Handlers

Implement `IQueryHandler<TQuery, TResult>`:

```csharp
[QueryHandler]
public class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderQueryHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<OrderDto?> HandleAsync(
        GetOrderQuery query, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(query.OrderId, ct);
        return order?.ToDto();
    }
}
```

### Dispatching Queries

Inject `IQueryDispatcher` and call `DispatchAsync`:

```csharp
public class OrderService(IQueryDispatcher queryDispatcher)
{
    public async Task<OrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        return await queryDispatcher.DispatchAsync(new GetOrderQuery(orderId), ct);
    }
}
```

Signature:

```csharp
ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
```

## Stream Queries

For queries that return sequences of results, Nexum provides first-class `IAsyncEnumerable<T>` support.

### Defining Stream Queries

```csharp
public record GetOrderEventsQuery(Guid OrderId) : IStreamQuery<OrderEventDto>;
```

### Stream Query Handlers

Implement `IStreamQueryHandler<TQuery, TResult>`:

```csharp
[StreamQueryHandler]
public class GetOrderEventsHandler : IStreamQueryHandler<GetOrderEventsQuery, OrderEventDto>
{
    private readonly IEventStore _eventStore;

    public GetOrderEventsHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async IAsyncEnumerable<OrderEventDto> HandleAsync(
        GetOrderEventsQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var evt in _eventStore.GetEventsAsync(query.OrderId, ct))
        {
            yield return evt.ToDto();
        }
    }
}
```

### Streaming Results

Use `IQueryDispatcher.StreamAsync`:

```csharp
public async Task StreamOrderEventsAsync(Guid orderId, CancellationToken ct)
{
    var query = new GetOrderEventsQuery(orderId);

    await foreach (var evt in queryDispatcher.StreamAsync(query, ct))
    {
        Console.WriteLine($"Event: {evt.Type} at {evt.Timestamp}");
    }
}
```

Signature:

```csharp
IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken ct = default);
```

## Handler Conventions

| Convention | Details |
|-----------|---------|
| Return type | `ValueTask<TResult>` (use `ValueTask.FromResult(value)` for sync paths) |
| Method name | `HandleAsync` |
| CancellationToken | Always accept as last parameter |
| Default lifetime | Scoped (one instance per DI scope / request) |
| Marker attribute | `[CommandHandler]`, `[QueryHandler]`, `[StreamQueryHandler]` for Source Generator discovery |
| Lifetime override | `[HandlerLifetime(NexumLifetime.Singleton)]` to change default |

## Handler Resolution

- Each command/query type maps to exactly **one** handler.
- If no handler is found, `NexumHandlerNotFoundException` is thrown.
- Handler types are cached in a thread-safe `ConcurrentDictionary<Type, Lazy<Type?>>`.
- All dispatchers are thread-safe and can be registered as singletons.

## Re-entrant Dispatch Protection

Nexum tracks dispatch depth via `AsyncLocal<int>`. If a handler dispatches another command/query, the depth increments. When it exceeds `MaxDispatchDepth` (default: 16), `NexumDispatchDepthExceededException` is thrown. This prevents infinite recursion.

Configure the limit:

```csharp
builder.Services.AddNexum(options =>
{
    options.MaxDispatchDepth = 32; // Increase if needed
});
```
