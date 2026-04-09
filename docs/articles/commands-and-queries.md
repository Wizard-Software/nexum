# Commands and Queries

Nexum enforces strict CQRS: commands, queries, and streaming queries each have their own contracts, handlers, dispatchers, and pipelines. The compiler prevents you from routing a command through a query dispatcher, and vice versa.

## Commands

A **command** expresses an intent to change state.

```csharp
// Command returning a value
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items) : ICommand<Guid>;

// Void command (no result)
public record DeleteOrderCommand(Guid OrderId) : IVoidCommand;
```

- `ICommand` — non-generic marker (enables exception handlers to constrain on all commands).
- `ICommand<TResult>` — command that returns `TResult`.
- `IVoidCommand : ICommand<Unit>` — commands without a result. `Unit` is Nexum's `void` equivalent for generic contexts (a `readonly struct` with zero size).

Because `IVoidCommand` inherits from `ICommand<Unit>`, a single `ICommandBehavior<TCommand, TResult>` interface covers both variants.

## Command handlers

```csharp
[CommandHandler]
public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;

    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        await _repo.SaveAsync(order, ct);
        return order.Id;
    }
}
```

- Handlers return `ValueTask<TResult>` — synchronous paths allocate nothing.
- The `[CommandHandler]` attribute is optional; it is consumed by the Source Generator for compile-time discovery.
- For void commands, implement `ICommandHandler<TCommand, Unit>` and return `Unit.Value`.

## Queries

A **query** expresses an intent to read state without modifying it.

```csharp
public record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>;
public record GetOrdersByCustomerQuery(string CustomerId) : IQuery<IReadOnlyList<OrderDto>>;
```

```csharp
[QueryHandler]
public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;
    public GetOrderQueryHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<OrderDto?> HandleAsync(GetOrderQuery query, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(query.OrderId, ct);
        return order?.ToDto();
    }
}
```

- `IQuery` — non-generic marker enabling value-type support in exception handlers.
- `IQuery<TResult>` — the normal query interface.

## Dispatchers

Nexum provides three dispatchers, registered automatically by `AddNexum()`:

```csharp
public interface ICommandDispatcher
{
    ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
}

public interface IQueryDispatcher
{
    ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);
    IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken ct = default);
}

public interface INotificationPublisher
{
    ValueTask PublishAsync<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification;
}
```

All dispatchers are thread-safe and can be registered as singletons. Handler resolution goes through a `ConcurrentDictionary<Type, Lazy<Type?>>` cache, so repeated dispatch of the same type is effectively free.

## Re-entrant dispatch protection

Nexum guards against unbounded re-entrant dispatch (handler A dispatches command B, which dispatches command A, ...) using an `AsyncLocal<int>` depth counter. The default maximum depth is 16 and is configurable via `NexumOptions.MaxDispatchDepth`.

## Naming conventions

| Contract | Method | Return type |
|----------|--------|-------------|
| `ICommand<T>` | `DispatchAsync` | `ValueTask<T>` |
| `IQuery<T>` | `DispatchAsync` | `ValueTask<T>` |
| `IStreamQuery<T>` | `StreamAsync` | `IAsyncEnumerable<T>` |
| `INotification` | `PublishAsync` | `ValueTask` |

All handlers implement `HandleAsync()` — never `Handle`, never `Execute`.
