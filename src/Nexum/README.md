# Nexum

CQRS runtime for .NET 10 — command, query, and notification dispatchers with `ValueTask`-based pipelines.

## What's inside

- **`CommandDispatcher`** — dispatches commands through the behavior pipeline
- **`QueryDispatcher`** — dispatches queries and stream queries
- **`NotificationPublisher`** — publishes domain events with configurable strategies
- Assembly scanning and manual handler registration
- Polymorphic handler resolution with thread-safe caching
- Re-entrant dispatch protection via `AsyncLocal<int>` depth guard

Works **standalone** without Source Generators — add `Nexum.SourceGenerators` for compile-time acceleration.

## Installation

```bash
dotnet add package Nexum
```

## Usage

```csharp
var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
var orderId = await dispatcher.DispatchAsync(new CreateOrder("cust-1", 99.99m));
```

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Commands and Queries](https://nexum.wizardsoftware.pl/articles/commands-and-queries.html) and [Architecture](https://nexum.wizardsoftware.pl/articles/architecture.html) for detailed usage and runtime internals.

## License

MIT
