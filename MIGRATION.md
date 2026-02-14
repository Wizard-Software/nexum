# Migration Guide: MediatR to Nexum

## Overview

This guide walks you through migrating an existing application from **MediatR** to **Nexum**. The migration can be done **gradually** -- both libraries can coexist in the same application, allowing you to migrate module by module at your own pace.

Nexum is a modern CQRS library for .NET 10+ / C# 14, designed as a MediatR successor. It introduces type-enforced Command/Query separation, `ValueTask<T>` return types, optional Source Generator acceleration, NativeAOT support, built-in OpenTelemetry, and dedicated exception handler interfaces.

---

## Key Differences

| Aspect | MediatR | Nexum |
|--------|---------|------|
| **Interfaces** | Shared `IRequest<T>` for commands and queries | Separate `ICommand<T>`, `IQuery<T>`, `IStreamQuery<T>` |
| **Return type** | `Task<T>` | `ValueTask<T>` |
| **Pipeline behaviors** | Global `IPipelineBehavior<,>` | Separate `ICommandBehavior<,>`, `IQueryBehavior<,>`, `IStreamQueryBehavior<,>` |
| **Dispatch** | `IMediator.Send()` | `ICommandDispatcher.DispatchAsync()`, `IQueryDispatcher.DispatchAsync()` |
| **Notifications** | `INotification` + `IMediator.Publish()` | `INotification` + `INotificationPublisher.PublishAsync(strategy)` |
| **Discovery** | Reflection (runtime only) | Hybrid: Source Generator (compile-time) + assembly scanning (runtime) |
| **Streams** | `IStreamRequest<T>` + `CreateStream()` | `IStreamQuery<T>` + `IQueryDispatcher.StreamAsync()` |
| **Exception handling** | Manual catch in behaviors | Dedicated `ICommandExceptionHandler<T,E>`, `IQueryExceptionHandler<T,E>` |

---

## Step-by-step Migration Plan

### Step 1: Install Nexum alongside MediatR

Add Nexum packages to your project. Both libraries can coexist in the same application -- they use different namespaces and different DI registrations.

```bash
dotnet add package Nexum
dotnet add package Nexum.Abstractions
dotnet add package Nexum.SourceGenerators    # optional, for compile-time acceleration
```

Register Nexum in your DI container alongside MediatR:

```csharp
// Existing MediatR registration -- keep as-is
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add Nexum registration
services.AddNexum();  // Source Generator auto-discovery
```

### Step 2: Write new handlers with Nexum interfaces

For all **new** features, use Nexum interfaces from the start:

```csharp
public record CreateOrderCommand(string CustomerId) : ICommand<Guid>;

[CommandHandler]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid();
        // ... business logic
        return orderId;
    }
}
```

### Step 3: Gradually rewrite existing handlers

Replace MediatR interfaces with Nexum equivalents one handler at a time:

- `IRequest<T>` becomes `ICommand<T>` or `IQuery<T>` (choose based on intent)
- `IRequestHandler<T, R>` becomes `ICommandHandler<T, R>` or `IQueryHandler<T, R>`
- `Task<T> Handle()` becomes `ValueTask<T> HandleAsync()`

### Step 4: Replace `IMediator` with Nexum dispatchers in controllers

Replace `IMediator` injection with the appropriate Nexum dispatcher:

```csharp
// Before
public class OrdersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var id = await mediator.Send(new CreateOrderCommand(request.CustomerId));
        return Ok(id);
    }
}

// After
public class OrdersController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request)
    {
        var id = await commandDispatcher.DispatchAsync(new CreateOrderCommand(request.CustomerId));
        return Ok(id);
    }
}
```

### Step 5: Rewrite `IPipelineBehavior` to Nexum behaviors

MediatR's global `IPipelineBehavior<,>` becomes type-specific behaviors in Nexum. This is one of the most significant changes -- Nexum enforces separate pipelines for commands, queries, and stream queries.

### Step 6: Remove MediatR

Once all handlers, behaviors, and dispatch calls have been migrated, remove MediatR:

```bash
dotnet remove package MediatR
dotnet remove package MediatR.Extensions.Microsoft.DependencyInjection
```

---

## Before / After Examples

### Command Definition

**MediatR:**

```csharp
public record CreateOrderCommand(string CustomerId) : IRequest<Guid>;
```

**Nexum:**

```csharp
public record CreateOrderCommand(string CustomerId) : ICommand<Guid>;
```

### Command Handler

**MediatR:**

```csharp
public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, Guid>
{
    public async Task<Guid> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        // ... business logic
        return orderId;
    }
}
```

**Nexum:**

```csharp
[CommandHandler]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
    {
        var orderId = Guid.NewGuid();
        // ... business logic
        return orderId;
    }
}
```

### Dispatch

**MediatR:**

```csharp
var result = await _mediator.Send(new CreateOrderCommand("C1"), ct);
```

**Nexum:**

```csharp
var result = await _commandDispatcher.DispatchAsync(new CreateOrderCommand("C1"), ct);
```

### Pipeline Behavior

**MediatR:**

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling {Type}", typeof(TRequest).Name);
        var response = await next();
        return response;
    }
}
```

**Nexum:**

```csharp
[BehaviorOrder(1)]
public class LoggingCommandBehavior<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly ILogger<LoggingCommandBehavior<TCommand, TResult>> _logger;

    public LoggingCommandBehavior(ILogger<LoggingCommandBehavior<TCommand, TResult>> logger)
        => _logger = logger;

    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        _logger.LogInformation("Handling {Type}", typeof(TCommand).Name);
        var result = await next(ct).ConfigureAwait(false);
        return result;
    }
}
```

Key differences:
- `IPipelineBehavior<TRequest, TResponse>` becomes `ICommandBehavior<TCommand, TResult>` (or `IQueryBehavior<TQuery, TResult>` for queries)
- `RequestHandlerDelegate<TResponse>` becomes `CommandHandlerDelegate<TResult>` (or `QueryHandlerDelegate<TResult>`)
- `next()` becomes `next(ct)` -- Nexum propagates `CancellationToken` through the delegate
- `.ConfigureAwait(false)` is required in Nexum (library code convention)
- `[BehaviorOrder(1)]` attribute controls pipeline execution order

### Registration

**MediatR:**

```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**Nexum:**

```csharp
services.AddNexum();  // Source Generator auto-discovery
services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>), order: 1);
```

### Notifications

**MediatR:**

```csharp
public record OrderCreatedEvent(Guid OrderId) : MediatR.INotification;

public class OrderCreatedHandler : MediatR.INotificationHandler<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent notification, CancellationToken ct)
    {
        // ... handle notification
        return Task.CompletedTask;
    }
}

// Dispatch -- always sequential
await _mediator.Publish(new OrderCreatedEvent(orderId), ct);
```

**Nexum:**

```csharp
public record OrderCreatedEvent(Guid OrderId) : Nexum.Abstractions.INotification;

[NotificationHandler]
public class OrderCreatedHandler : INotificationHandler<OrderCreatedEvent>
{
    public ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct = default)
    {
        // ... handle notification
        return ValueTask.CompletedTask;
    }
}

// Dispatch -- choose a publish strategy (or omit for default)
await _notificationPublisher.PublishAsync(new OrderCreatedEvent(orderId), PublishStrategy.Parallel, ct);
```

Key differences:
- `MediatR.INotification` becomes `Nexum.Abstractions.INotification`
- `Task Handle()` becomes `ValueTask HandleAsync()`
- `IMediator.Publish()` becomes `INotificationPublisher.PublishAsync(notification, strategy, ct)`
- Nexum supports 4 publish strategies: `Sequential`, `Parallel`, `StopOnException`, `FireAndForget`

---

## Breaking Changes Checklist

When migrating each handler, apply these changes:

- [ ] `Task<T>` --> `ValueTask<T>` -- handlers and behaviors must return `ValueTask<T>`
- [ ] `Handle()` --> `HandleAsync()` -- method name change
- [ ] `IRequest<T>` --> `ICommand<T>` / `IQuery<T>` -- separate interfaces based on intent
- [ ] `IPipelineBehavior<,>` --> `ICommandBehavior<,>` / `IQueryBehavior<,>` -- separate pipelines
- [ ] `IMediator.Send()` --> `ICommandDispatcher.DispatchAsync()` / `IQueryDispatcher.DispatchAsync()`
- [ ] `next()` --> `next(ct)` -- `CancellationToken` is a delegate parameter in Nexum
- [ ] `RequestHandlerDelegate<T>` --> `CommandHandlerDelegate<T>` / `QueryHandlerDelegate<T>`
- [ ] `IMediator.Publish()` --> `INotificationPublisher.PublishAsync(notification, strategy, ct)`

---

## Coexistence Strategy

MediatR and Nexum can run simultaneously in the same application. The recommended approach is to migrate **module by module**:

1. **New modules** --> implement with Nexum from the start
2. **Existing modules** --> continue using MediatR (no changes needed)
3. **Gradual rewrite** --> migrate one module at a time from MediatR to Nexum
4. **After full migration** --> remove MediatR packages entirely

### Recommended Migration Timeline

| Phase | Scope | Milestone |
|-------|-------|-----------|
| **1. Setup** | Install Nexum, configure DI, write new handlers with Nexum | Nexum runs alongside MediatR |
| **2. Coexistence** | Migrate controllers, rewrite handlers module by module | > 50% of handlers on Nexum |
| **3. Cutover** | Rewrite behaviors, exception handlers, remove adapter | 100% of handlers on Nexum |
| **4. Cleanup** | Remove MediatR packages, clean up unused references | Clean Nexum-only installation |

**Tips for a smooth migration:**

- Start with simple, isolated handlers (no behaviors, no notifications)
- Migrate read operations (queries) before write operations (commands) -- queries are typically simpler
- Test each migrated handler individually before proceeding
- Keep both DI registrations active until the migration is complete

---

## Compatibility Matrix

| Feature | Nexum | MediatR |
|---------|------|---------|
| **Command/Query separation** | Yes (type-enforced via `ICommand<T>`, `IQuery<T>`) | No (shared `IRequest<T>`) |
| **Return type** | `ValueTask<T>` (lower allocation) | `Task<T>` |
| **Source Generators** | Yes (optional, tiered: DI registration, compiled pipelines, interceptors) | No |
| **NativeAOT** | Yes (with Source Generator) | No (relies on runtime reflection) |
| **Pipeline behaviors** | Per-type (`ICommandBehavior`, `IQueryBehavior`, `IStreamQueryBehavior`) | Global (`IPipelineBehavior`) |
| **Exception handlers** | Dedicated interfaces with two-axis resolution (command type + exception type) | Manual catch in behaviors |
| **Publish strategies** | 4 strategies: Sequential, Parallel, StopOnException, FireAndForget | Sequential only |
| **Stream queries** | `IAsyncEnumerable<T>` first-class via `IStreamQuery<T>` + `StreamAsync()` | Limited (`IStreamRequest<T>` + `CreateStream()`) |
| **OpenTelemetry** | Built-in (optional `Nexum.OpenTelemetry` package) | External / manual |
| **Result pattern** | Native (optional `Nexum.Results` package) | External |

---

## Future: Migration Package

A `Nexum.Migration.MediatR` compatibility package with an adapter pattern is planned for future releases. This package will enable automatic bridging of MediatR handlers through Nexum dispatchers during the migration period, allowing you to dispatch through Nexum's pipeline while existing MediatR handlers continue to work unchanged.

Check the [Nexum repository](https://github.com/asawicki/flux) for updates on this package.
