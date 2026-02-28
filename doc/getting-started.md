# Getting Started

## Requirements

| Requirement | Minimum Version | Notes |
|-------------|----------------|-------|
| .NET SDK | 10.0 | Target framework: `net10.0` |
| C# | 14 | Automatic with .NET 10 SDK |

## Installation

Install the core packages:

```bash
dotnet add package Nexum.Abstractions
dotnet add package Nexum
dotnet add package Nexum.Extensions.DependencyInjection
```

For compile-time handler registration (recommended):

```bash
dotnet add package Nexum.SourceGenerators
```

Optional packages:

```bash
dotnet add package Nexum.OpenTelemetry           # Distributed tracing and metrics
dotnet add package Nexum.Results                  # Result<T> pattern
dotnet add package Nexum.Extensions.AspNetCore    # Endpoint mapping, Problem Details
dotnet add package Nexum.Batching                 # Automatic query batching
```

Or add via `PackageReference`:

```xml
<ItemGroup>
    <PackageReference Include="Nexum.Abstractions" Version="1.0.0" />
    <PackageReference Include="Nexum" Version="1.0.0" />
    <PackageReference Include="Nexum.Extensions.DependencyInjection" Version="1.0.0" />
    <PackageReference Include="Nexum.SourceGenerators" Version="1.0.0" />
</ItemGroup>
```

## Minimal Example

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

## Step by Step

### 1. Define a Command

A command represents an intent to modify state. It implements `ICommand<TResult>`.

```csharp
// Command returning a value
public record CreateOrderCommand(
    string CustomerId,
    List<OrderItemDto> Items) : ICommand<Guid>;

// Void command (no return value)
public record DeleteOrderCommand(Guid OrderId) : IVoidCommand;
```

**Conventions:**
- Use `record` (or `record struct` for fewer allocations).
- `ICommand<TResult>` for commands that return a value.
- `IVoidCommand` for commands with no result (alias for `ICommand<Unit>`).

### 2. Implement a Handler

A handler contains your business logic. It implements `ICommandHandler<TCommand, TResult>`.

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

The `[CommandHandler]` attribute is needed for Source Generator discovery. Without it, use manual or assembly-scanning registration (see [Configuration](configuration.md)).

### 3. Define a Query

A query represents an intent to read state. It implements `IQuery<TResult>`.

```csharp
public record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>;

public record GetOrdersQuery(string CustomerId) : IQuery<IReadOnlyList<OrderDto>>;
```

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

### 4. Register in DI

```csharp
var builder = WebApplication.CreateBuilder(args);

// With Source Generator (recommended)
builder.Services.AddNexum();

// Without Source Generator (assembly scanning)
builder.Services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);

// With configuration
builder.Services.AddNexum(configure: options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Sequential;
    options.MaxDispatchDepth = 16;
});
```

### 5. Dispatch Commands and Queries

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var orderId = await dispatcher.DispatchAsync(command, ct);
    return Results.Created($"/orders/{orderId}", new { Id = orderId });
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    IQueryDispatcher dispatcher,
    CancellationToken ct) =>
{
    var order = await dispatcher.DispatchAsync(new GetOrderQuery(id), ct);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});
```

Commands go through `ICommandDispatcher`, queries through `IQueryDispatcher`. The compiler enforces this separation -- you cannot dispatch a command through a query dispatcher.

## Next Steps

- [Commands and Queries](commands-and-queries.md) -- Full reference for all CQRS types
- [Pipeline Behaviors](behaviors.md) -- Add cross-cutting concerns like validation and logging
- [Notifications](notifications.md) -- Publish domain events
- [Configuration](configuration.md) -- Customize options and DI lifetimes
