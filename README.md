# Nexum

Modern CQRS library for .NET -- compile-time safe, zero-reflection, observable.

## Why Nexum?

Nexum is a next-generation CQRS (Command Query Responsibility Segregation) library for .NET 10, designed as a successor to MediatR with focus on performance, type safety, and observability.

| Feature | MediatR | Nexum |
|---------|---------|------|
| Command/Query separation | Shared `IRequest<T>` | Separate `ICommand`, `IQuery`, `IStreamQuery` |
| Handler resolution | Runtime reflection | Compile-time Source Generators |
| Return type | `Task<T>` | `ValueTask<T>` (zero-alloc for sync paths) |
| Pipeline behaviors | Global (`IPipelineBehavior`) | Separate `ICommandBehavior` / `IQueryBehavior` |
| Observability | External packages | Built-in OpenTelemetry `ActivitySource` |
| Streaming | Limited `CreateStream` | First-class `IAsyncEnumerable<T>` support |

## Key Features

- **Strict CQRS** -- Commands and Queries are separate types with dedicated dispatchers and pipelines.
- **Zero reflection** -- Source Generators handle all handler registration and pipeline wiring at compile time.
- **Minimal allocations** -- `ValueTask<T>` as default return type eliminates `Task` allocations on synchronous paths.
- **Built-in OpenTelemetry** -- Every dispatch automatically creates an `Activity` for full observability out of the box.
- **Async streams** -- `IAsyncEnumerable<T>` as a first-class citizen via `IStreamQuery<T>`.
- **Flexible event publishing** -- Sequential, Parallel, StopOnException, and FireAndForget strategies.
- **Optional Result Pattern** -- Native `Result<T, TError>` with adapter support for external libraries.
- **DI agnostic** -- Automatic registration via Source Generators, works with any DI container.

## Getting Started

### Requirements

| Requirement | Minimum Version | Notes |
|-------------|----------------|-------|
| .NET SDK | 10.0 | Target framework: `net10.0` |
| C# | 14 | Automatic with .NET 10 SDK |

### Installation

**NuGet CLI:**

```bash
# Core packages
dotnet add package Nexum.Abstractions
dotnet add package Nexum
dotnet add package Nexum.Extensions.DependencyInjection

# Recommended: Source Generator for compile-time registration
dotnet add package Nexum.SourceGenerators

# Optional: OpenTelemetry, Result Pattern, ASP.NET Core
dotnet add package Nexum.OpenTelemetry
dotnet add package Nexum.Results
dotnet add package Nexum.Extensions.AspNetCore
```

**PackageReference (.csproj):**

```xml
<ItemGroup>
    <PackageReference Include="Nexum.Abstractions" Version="1.0.0" />
    <PackageReference Include="Nexum" Version="1.0.0" />
    <PackageReference Include="Nexum.Extensions.DependencyInjection" Version="1.0.0" />
    <PackageReference Include="Nexum.SourceGenerators" Version="1.0.0" />
</ItemGroup>
```

### Minimal Example

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

app.MapPost("/orders", async (CreateOrderCommand cmd, ICommandDispatcher dispatcher, CancellationToken ct) =>
{
    var orderId = await dispatcher.DispatchAsync(cmd, ct);
    return Results.Created($"/orders/{orderId}", new { Id = orderId });
});

app.Run();
```

## Step by Step

### 1. Define a Command

A command represents an intent to modify state. It implements `ICommand<TResult>` with the result type.

```csharp
// Command returning a Guid (order ID)
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
[CommandHandler] // Marker attribute for Source Generator discovery (optional)
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

**Conventions:**
- The `[CommandHandler]` attribute is optional -- it is needed when using the Source Generator for compile-time discovery.
- Handlers return `ValueTask<TResult>` (not `Task<TResult>`).
- For synchronous operations, use `ValueTask.FromResult(value)`.

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

// Mode A: With Source Generator (recommended)
builder.Services.AddNexum();

// Mode B: Without Source Generator (assembly scanning)
builder.Services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);

// Optional configuration
builder.Services.AddNexum(configure: options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Sequential;
    options.MaxDispatchDepth = 16;
});
```

The full `AddNexum` signature:

```csharp
public static IServiceCollection AddNexum(
    this IServiceCollection services,
    Action<NexumOptions>? configure = null,
    params Assembly[] assemblies)
```

- When the `Nexum.SourceGenerators` package is installed, `AddNexum()` discovers handlers at compile time with zero reflection.
- When assemblies are provided explicitly, runtime assembly scanning is used instead.

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

Commands are dispatched via `ICommandDispatcher` and queries via `IQueryDispatcher`. The compiler enforces separation -- you cannot dispatch a command through `IQueryDispatcher` or vice versa.

## Advanced Features

### Pipeline Behaviors

Nexum supports separate pipeline behaviors for commands and queries using the Russian doll model. Each behavior wraps the next, enabling cross-cutting concerns like validation, logging, and transactions.

- `ICommandBehavior<TCommand, TResult>` -- wraps command execution.
- `IQueryBehavior<TQuery, TResult>` -- wraps query execution.
- `[BehaviorOrder(int)]` -- controls execution order (lower values execute first).

Behaviors are type-safe and scoped: a command validation behavior will never accidentally run in the query pipeline.

### Notifications

Domain events are modeled as `INotification` and dispatched via `INotificationPublisher.PublishAsync()`. Nexum supports four publish strategies:

- **Sequential** -- handlers execute one after another.
- **Parallel** -- handlers execute concurrently via `Task.WhenAll`.
- **StopOnException** -- sequential execution stops on the first exception.
- **FireAndForget** -- notifications are published in the background via a bounded channel and `BackgroundService`. Each handler runs in its own `IServiceScope`. Exceptions are routed to `INotificationExceptionHandler` instead of propagating to the caller.

The default strategy is configured via `NexumOptions.DefaultPublishStrategy` and can be overridden per-publish call.

### Stream Queries

For queries that return sequences of results, Nexum provides first-class `IAsyncEnumerable<T>` support via `IStreamQuery<TResult>`. Stream queries are dispatched with `IQueryDispatcher.StreamAsync()` and support dedicated `IStreamQueryBehavior<TQuery, TResult>` pipeline behaviors.

### Exception Handlers

Nexum provides structured exception handling outside the pipeline via `ICommandExceptionHandler<TCommand, TException>` and `IQueryExceptionHandler<TQuery, TException>`. Exception handlers are side-effect only (logging, metrics, alerts) -- the dispatcher always re-throws after invoking them. Handlers are resolved most-specific-first based on both the command/query type and the exception type.

### OpenTelemetry Integration

The `Nexum.OpenTelemetry` package adds automatic distributed tracing and metrics to every dispatch. Each command, query, and notification creates an `Activity` with structured tags, enabling full observability through any OpenTelemetry-compatible backend. No code changes required -- just add the package and configure your `ActivitySource`.

### Result Pattern

The `Nexum.Results` package provides a native `Result<T, TError>` type for explicit error handling without exceptions. Results use composition (not inheritance) and integrate naturally with Nexum commands and queries. A convenience alias `Result<T>` defaults the error type to `NexumError`.

### ASP.NET Core Integration

The `Nexum.Extensions.AspNetCore` package provides middleware, endpoint routing helpers, and Problem Details integration for seamless use of Nexum in ASP.NET Core applications.

## Packages

| Package | Description |
|---------|-------------|
| `Nexum.Abstractions` | Core interfaces (`ICommand`, `IQuery`, `INotification`, etc.). Zero dependencies. |
| `Nexum.SourceGenerators` | Roslyn Source Generators for compile-time handler registration and validation. |
| `Nexum` | Dispatchers (`CommandDispatcher`, `QueryDispatcher`, `NotificationPublisher`), pipeline middleware. |
| `Nexum.OpenTelemetry` | `ActivitySource`, metrics, `System.Diagnostics` integration. |
| `Nexum.Results` | Optional `Result<T, TError>`, `NexumError`, `IResultAdapter`. |
| `Nexum.Results.FluentValidation` | FluentValidation integration for the Result pattern. |
| `Nexum.Extensions.DependencyInjection` | `IServiceCollection.AddNexum()` extensions. |
| `Nexum.Extensions.AspNetCore` | Middleware, endpoint routing, Problem Details integration. |
| `Nexum.Batching` | Automatic query batching with configurable windows and deduplication. |

## Benchmarks

Nexum with Source Generators is **2x faster** than MediatR with **zero allocations** for simple commands. With pipeline behaviors, the advantage grows to **1.7x faster** with **3.8x less memory**. For notifications with 5 handlers, Nexum is **2.2x faster** with **28x less memory**.

Even without Source Generators, the Nexum Runtime dispatcher is **1.5x faster** than MediatR with zero allocations.

### Nexum vs MediatR

> BenchmarkDotNet v0.15.8, macOS Tahoe, Apple M3 Max, .NET SDK 10.0.103, .NET 10.0.3 (10.0.326.7603)
>
> Measured February 2026

#### Simple Command Dispatch (no behaviors)

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Nexum Source Generator | 18.96 ns | 0 B |
| Nexum Runtime | 25.84 ns | 0 B |
| MediatR | 39.19 ns | 208 B |

**Nexum SG vs MediatR:** 2.1x faster, zero allocations.
**Nexum Runtime vs MediatR:** 1.5x faster, zero allocations.

#### Command Dispatch with 3 Pipeline Behaviors

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Nexum Source Generator | 72.24 ns | 192 B |
| Nexum Runtime | 88.73 ns | 408 B |
| MediatR | 121.37 ns | 736 B |

**Nexum SG vs MediatR:** 1.7x faster, 3.8x less memory.
**Nexum Runtime vs MediatR:** 1.4x faster, 1.8x less memory.

#### Notifications (5 handlers, Sequential)

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Nexum Source Generator | 64.40 ns | 32 B |
| Nexum Runtime | 64.40 ns | 32 B |
| MediatR | 143.35 ns | 896 B |

**Nexum vs MediatR:** 2.2x faster, 28x less memory.

### Source Generator Tiers

Nexum Source Generator uses a tiered architecture. Each tier builds on the previous one, progressively eliminating overhead:

- **Tier 1 (Runtime)** -- reflection-based handler resolution with polymorphic caching.
- **Tier 2 (Compiled Pipeline)** -- monomorphized dispatch via source-generated delegates, bypasses pipeline builder.
- **Tier 3 (Interceptors)** -- Roslyn interceptors replace `DispatchAsync` call sites at compile time, eliminating virtual dispatch.

#### Simple Command Dispatch -- Tier Comparison

| Tier | Mean | Allocated | vs Runtime |
|------|-----:|----------:|-----------:|
| Tier 3 -- Interceptor | 16.55 ns | 0 B | 1.52x faster |
| Tier 2 -- Compiled Pipeline | 19.04 ns | 0 B | 1.32x faster |
| Tier 1 -- Runtime | 25.19 ns | 0 B | baseline |

All three tiers achieve **zero allocations**. Tier 3 interceptors are the fastest path -- **34% faster** than Runtime and **13% faster** than Tier 2 compiled pipelines.

## Migration from MediatR

Nexum is designed as a drop-in evolution from MediatR. The migration can be done gradually -- both libraries can coexist in the same project during the transition. Key changes include replacing `IRequest<T>` with `ICommand<T>`/`IQuery<T>`, `Task<T>` with `ValueTask<T>`, `Handle()` with `HandleAsync()`, and `Send()` with `DispatchAsync()`.

See [MIGRATION.md](MIGRATION.md) for a complete step-by-step migration guide with before/after code examples.

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

- [**Getting Started**](https://nexum.wizardsoftware.pl/articles/getting-started.html) -- installation and your first command.
- [**Commands and Queries**](https://nexum.wizardsoftware.pl/articles/commands-and-queries.html) -- core CQRS types, handlers, and dispatchers.
- [**Notifications**](https://nexum.wizardsoftware.pl/articles/notifications.html) -- domain events and publish strategies.
- [**Stream Queries**](https://nexum.wizardsoftware.pl/articles/streams.html) -- first-class `IAsyncEnumerable<T>` support.
- [**Pipeline Behaviors**](https://nexum.wizardsoftware.pl/articles/behaviors.html) -- cross-cutting concerns via the Russian doll model.
- [**Source Generators**](https://nexum.wizardsoftware.pl/articles/source-generators.html) -- tiered compile-time acceleration.
- [**Dependency Injection**](https://nexum.wizardsoftware.pl/articles/dependency-injection.html) -- `AddNexum()`, lifetimes, manual registration.
- [**ASP.NET Core Integration**](https://nexum.wizardsoftware.pl/articles/aspnetcore-integration.html) -- minimal APIs, middleware, Problem Details.
- [**OpenTelemetry**](https://nexum.wizardsoftware.pl/articles/opentelemetry.html) -- distributed tracing and metrics.
- [**Result Pattern**](https://nexum.wizardsoftware.pl/articles/results.html) -- explicit error handling with `Result<T, TError>`.
- [**Batching**](https://nexum.wizardsoftware.pl/articles/batching.html) -- automatic query batching and deduplication.
- [**Testing**](https://nexum.wizardsoftware.pl/articles/testing.html) -- `NexumTestHost`, fake dispatchers, behavior isolation.
- [**Migration from MediatR**](https://nexum.wizardsoftware.pl/articles/migration-from-mediatr.html) -- gradual migration guide.
- [**Architecture**](https://nexum.wizardsoftware.pl/articles/architecture.html) -- internals, thread safety, package graph.
- [**API Reference**](https://nexum.wizardsoftware.pl/api/) -- complete public API surface.

## License

[MIT](LICENSE)
