# Getting Started

This guide takes you from a fresh .NET project to dispatching your first Nexum command.

## Requirements

| Requirement | Minimum | Notes |
|-------------|---------|-------|
| .NET SDK    | 10.0    | Target framework: `net10.0` |
| C#          | 14      | Automatic with .NET 10 SDK |

## Installation

Install the core packages from NuGet:

```bash
# Core runtime (works standalone, no source generator required)
dotnet add package Nexum.Abstractions
dotnet add package Nexum
dotnet add package Nexum.Extensions.DependencyInjection

# Recommended: Source Generator for compile-time registration
dotnet add package Nexum.SourceGenerators

# Optional
dotnet add package Nexum.OpenTelemetry
dotnet add package Nexum.Results
dotnet add package Nexum.Extensions.AspNetCore
```

Or via `<PackageReference>`:

```xml
<ItemGroup>
  <PackageReference Include="Nexum.Abstractions" Version="1.0.0" />
  <PackageReference Include="Nexum" Version="1.0.0" />
  <PackageReference Include="Nexum.Extensions.DependencyInjection" Version="1.0.0" />
  <PackageReference Include="Nexum.SourceGenerators" Version="1.0.0" />
</ItemGroup>
```

## Minimal example

```csharp
using Nexum.Abstractions;

// 1. Define a command
public record CreateOrderCommand(string CustomerId, List<string> Items) : ICommand<Guid>;

// 2. Implement a handler
[CommandHandler]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        // ... business logic ...
        return ValueTask.FromResult(orderId);
    }
}

// 3. Register in DI and dispatch
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNexum(); // Source Generator auto-discovery

var app = builder.Build();

app.MapPost("/orders", async (
    CreateOrderCommand cmd,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var orderId = await dispatcher.DispatchAsync(cmd, ct);
    return Results.Created($"/orders/{orderId}", new { Id = orderId });
});

app.Run();
```

## What happened here?

1. `CreateOrderCommand` is a plain record implementing `ICommand<Guid>` — the generic parameter is the result type.
2. `CreateOrderHandler` implements `ICommandHandler<CreateOrderCommand, Guid>` and returns `ValueTask<Guid>` (zero allocations on synchronous paths).
3. `[CommandHandler]` is a marker attribute used by the optional Source Generator for compile-time discovery. Without the generator, use assembly scanning instead: `AddNexum(assemblies: typeof(CreateOrderHandler).Assembly)`.
4. `ICommandDispatcher.DispatchAsync` resolves the correct handler through a polymorphic cache and runs the pipeline (behaviors → handler).

## Next steps

- [Commands and Queries](commands-and-queries.md) — full details on the CQRS contracts.
- [Dependency Injection](dependency-injection.md) — `AddNexum()` options and handler lifetimes.
- [Source Generators](source-generators.md) — enabling Tier 2 / Tier 3 acceleration.
- [Pipeline Behaviors](behaviors.md) — adding validation, logging, and transactions.
