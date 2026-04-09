# Nexum.Testing

Testing helpers for [Nexum](https://github.com/Wizard-Software/nexum) CQRS.

## What's inside

- **`NexumTestHost`** — lightweight test host with in-memory DI
- **Fake dispatchers** — capture dispatched commands/queries for assertions
- **Assertion extensions** — fluent assertions for verifying dispatches and handler behavior

## Installation

```bash
dotnet add package Nexum.Testing
```

## Usage

```csharp
await using var host = new NexumTestHost(services =>
{
    services.AddNexum(o => o.AddHandlersFromAssemblyOf<CreateOrderHandler>());
});

var result = await host.DispatchAsync(new CreateOrder("cust-1", 99.99m));
```

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Testing](https://nexum.wizardsoftware.pl/articles/testing.html) for detailed usage.

## License

MIT
