# Nexum.Abstractions

Core CQRS abstractions for the [Nexum](https://github.com/Wizard-Software/nexum) library. Zero dependencies.

## What's inside

- **`ICommand<TResult>`** / **`IVoidCommand`** — write-side intents
- **`IQuery<TResult>`** — read-side intents
- **`IStreamQuery<TResult>`** — streaming reads via `IAsyncEnumerable<T>`
- **`INotification`** — domain events with publish strategies (Sequential, Parallel, StopOnException, FireAndForget)
- **`ICommandBehavior<T,R>`** / **`IQueryBehavior<T,R>`** / **`IStreamQueryBehavior<T,R>`** — pipeline behaviors
- **`Unit`** — void equivalent for generic contexts
- Dispatcher interfaces: `ICommandDispatcher`, `IQueryDispatcher`, `INotificationPublisher`
- Marker attributes: `[CommandHandler]`, `[QueryHandler]`, `[BehaviorOrder]`, `[HandlerLifetime]`

## Installation

```bash
dotnet add package Nexum.Abstractions
```

## Usage

Reference this package in your **domain layer** to define commands, queries, and handlers without taking a dependency on the runtime.

```csharp
public sealed record CreateOrder(string CustomerId, decimal Total) : ICommand<OrderId>;

[CommandHandler]
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderId>
{
    public ValueTask<OrderId> HandleAsync(CreateOrder command, CancellationToken ct) => ...;
}
```

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Commands and Queries](https://nexum.wizardsoftware.pl/articles/commands-and-queries.html) for detailed usage of `ICommand`, `IQuery`, `INotification`, and related contracts.

## License

MIT
