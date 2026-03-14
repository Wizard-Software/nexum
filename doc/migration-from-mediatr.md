# Migrating from MediatR to Nexum

This guide walks you through migrating an existing MediatR codebase to Nexum. The migration is designed to be gradual and fully reversible -- you can migrate one handler at a time while keeping both systems running in parallel.

## Overview

### Migration Strategies

**Handler-by-handler (recommended)** -- Add Nexum alongside MediatR, then migrate handlers one at a time. Safe for production systems, zero downtime, easy to roll back at any step.

**Big-bang** -- Replace everything in one commit. Suitable for small projects with a handful of handlers.

This guide focuses on the handler-by-handler approach.

### Key Differences from MediatR

| Concept | MediatR | Nexum |
|---------|---------|-------|
| Request types | `IRequest<T>` unifies commands and queries | `ICommand<T>` and `IQuery<T>` are separate types |
| Handlers | `IRequestHandler<T,R>` for both | `ICommandHandler<T,R>` and `IQueryHandler<T,R>` |
| Return type | `Task<T>` | `ValueTask<T>` |
| Pipelines | One `IPipelineBehavior<,>` for everything | Separate `ICommandBehavior<,>` and `IQueryBehavior<,>` |
| Method name | `Handle()` | `HandleAsync()` |
| Dispatching | `IMediator.Send()` | `ICommandDispatcher.DispatchAsync()` / `IQueryDispatcher.DispatchAsync()` |

---

## Prerequisites

Install the migration compatibility package alongside your existing MediatR dependency:

```bash
dotnet add package Nexum.Migration.MediatR
```

The package includes:
- **MediatR adapters** -- bridge MediatR handlers so they are callable through Nexum dispatchers without any code changes.
- **Migration analyzers** (NEXUMM001-NEXUMM003) -- IDE hints that highlight types still using MediatR-only interfaces.

---

## Phase 1: Setup and Coexistence

### Step 1.1: Configure DI

Replace `AddMediatR()` with `AddNexumWithMediatRCompat()`. This single call registers both systems and wires up the adapters automatically.

**Before:**

```csharp
// Program.cs
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**After:**

```csharp
// Program.cs
builder.Services.AddNexumWithMediatRCompat(
    assemblies: typeof(Program).Assembly);
```

After this change:
- `IMediator.Send()` continues to work -- all existing MediatR handler registrations are preserved.
- `ICommandDispatcher.DispatchAsync()` and `IQueryDispatcher.DispatchAsync()` are now available.
- MediatR handlers that handle dual-interface types are automatically bridged by adapters.

You can also pass configuration for both systems:

```csharp
builder.Services.AddNexumWithMediatRCompat(
    configureNexum: options =>
    {
        options.DefaultPublishStrategy = PublishStrategy.Sequential;
        options.MaxDispatchDepth = 16;
    },
    configureMediatR: cfg => cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)),
    assemblies: typeof(Program).Assembly);
```

### Step 1.2: Add Nexum interfaces to your request types

For each request type, add the corresponding Nexum interface. This step does **not** require changing any handlers -- the existing MediatR handlers continue to run through adapters.

**Command (state-modifying request):**

```csharp
// Before
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items)
    : MediatR.IRequest<Guid>;

// After -- add ICommand<Guid>, keep IRequest<Guid> during transition
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items)
    : MediatR.IRequest<Guid>, ICommand<Guid>;
```

**Query (read-only request):**

```csharp
// Before
public record GetOrderQuery(Guid OrderId)
    : MediatR.IRequest<OrderDto?>;

// After -- add IQuery<OrderDto?>, keep IRequest<OrderDto?> during transition
public record GetOrderQuery(Guid OrderId)
    : MediatR.IRequest<OrderDto?>, IQuery<OrderDto?>;
```

**Notification:**

```csharp
// Before
public record OrderCreatedEvent(Guid OrderId, string CustomerId)
    : MediatR.INotification;

// After -- add Nexum INotification, keep MediatR.INotification during transition
public record OrderCreatedEvent(Guid OrderId, string CustomerId)
    : MediatR.INotification, Nexum.Abstractions.INotification;
```

### Step 1.3: Verify both dispatchers work

At this point, both systems dispatch to the same underlying MediatR handlers via adapters. You can verify this by dispatching the same request through both interfaces:

```csharp
// Both of these call the same MediatR handler
var id1 = await mediator.Send(new CreateOrderCommand("C-1", items), ct);
var id2 = await commandDispatcher.DispatchAsync(new CreateOrderCommand("C-1", items), ct);
```

Run your existing test suite -- all tests should pass unchanged.

---

## Phase 2: Migrate Handlers One by One

Migrate handlers in any order. After each migration, run your tests before moving to the next handler.

### Step 2.1: Migrate command handlers

**Before (MediatR handler):**

```csharp
public class CreateOrderHandler
    : IRequestHandler<CreateOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;

    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<Guid> Handle(
        CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = Order.Create(request.CustomerId, request.Items);
        await _repo.SaveAsync(order, cancellationToken);
        return order.Id;
    }
}
```

**After (Nexum handler):**

```csharp
[CommandHandler]
public class CreateOrderHandler
    : ICommandHandler<CreateOrderCommand, Guid>
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

Changes:
- Interface: `IRequestHandler<CreateOrderCommand, Guid>` → `ICommandHandler<CreateOrderCommand, Guid>`
- Add `[CommandHandler]` attribute for Source Generator discovery
- Method: `Handle(request, cancellationToken)` → `HandleAsync(command, ct)`
- Return type: `Task<Guid>` → `ValueTask<Guid>`

Once the native Nexum handler is registered, the adapter is no longer used (native handlers take priority).

**Void commands** use `Unit` as the result type:

```csharp
// Before
public class DeleteOrderHandler : IRequestHandler<DeleteOrderCommand>
{
    public async Task Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        await _repo.DeleteAsync(request.OrderId, cancellationToken);
    }
}

// After -- IVoidCommand is ICommand<Unit>
[CommandHandler]
public class DeleteOrderHandler : ICommandHandler<DeleteOrderCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(DeleteOrderCommand command, CancellationToken ct)
    {
        await _repo.DeleteAsync(command.OrderId, ct);
        return Unit.Value;
    }
}
```

### Step 2.2: Migrate query handlers

**Before (MediatR handler):**

```csharp
public class GetOrderHandler
    : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<OrderDto?> Handle(
        GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _repo.GetByIdAsync(request.OrderId, cancellationToken);
        return order?.ToDto();
    }
}
```

**After (Nexum handler):**

```csharp
[QueryHandler]
public class GetOrderHandler
    : IQueryHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<OrderDto?> HandleAsync(
        GetOrderQuery query, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(query.OrderId, ct);
        return order?.ToDto();
    }
}
```

Changes mirror the command migration: interface, attribute, method name, and return type.

### Step 2.3: Migrate pipeline behaviors

Nexum separates command and query pipelines. A MediatR `IPipelineBehavior<,>` that applies to both commands and queries splits into two Nexum behaviors -- one per pipeline. In practice, most behaviors apply to one or the other; split accordingly.

**Before (MediatR behavior, applies to all requests):**

```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        var result = await next();
        _logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);
        return result;
    }
}
```

**After (Nexum command behavior):**

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
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling {CommandType}", typeof(TCommand).Name);
        var result = await next(ct).ConfigureAwait(false);
        _logger.LogInformation("Handled {CommandType}", typeof(TCommand).Name);
        return result;
    }
}
```

**After (Nexum query behavior):**

```csharp
[BehaviorOrder(1)]
public class LoggingQueryBehavior<TQuery, TResult>
    : IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly ILogger<LoggingQueryBehavior<TQuery, TResult>> _logger;

    public LoggingQueryBehavior(ILogger<LoggingQueryBehavior<TQuery, TResult>> logger)
        => _logger = logger;

    public async ValueTask<TResult> HandleAsync(
        TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct)
    {
        _logger.LogInformation("Handling {QueryType}", typeof(TQuery).Name);
        var result = await next(ct).ConfigureAwait(false);
        _logger.LogInformation("Handled {QueryType}", typeof(TQuery).Name);
        return result;
    }
}
```

Key differences:
- Interface: `IPipelineBehavior<TRequest, TResponse>` → `ICommandBehavior<TCommand, TResult>` or `IQueryBehavior<TQuery, TResult>`
- The `next` delegate: `RequestHandlerDelegate<TResponse>` (no parameters) → `CommandHandlerDelegate<TResult>` / `QueryHandlerDelegate<TResult>` (accepts `CancellationToken` -- always forward it via `next(ct)`)
- Add `[BehaviorOrder(n)]` to control execution order explicitly
- Return type: `Task<TResponse>` → `ValueTask<TResult>`

Register the new behaviors:

```csharp
builder.Services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>), order: 1);
builder.Services.AddNexumBehavior(typeof(LoggingQueryBehavior<,>), order: 1);
```

### Step 2.4: Migrate notification handlers

**Before (MediatR notification handler):**

```csharp
public class SendOrderConfirmationEmail
    : MediatR.INotificationHandler<OrderCreatedEvent>
{
    private readonly IEmailService _email;

    public SendOrderConfirmationEmail(IEmailService email) => _email = email;

    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        await _email.SendOrderConfirmationAsync(
            notification.OrderId, notification.CustomerId, cancellationToken);
    }
}
```

**After (Nexum notification handler):**

```csharp
[NotificationHandler]
public class SendOrderConfirmationEmail
    : Nexum.Abstractions.INotificationHandler<OrderCreatedEvent>
{
    private readonly IEmailService _email;

    public SendOrderConfirmationEmail(IEmailService email) => _email = email;

    public async ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct)
    {
        await _email.SendOrderConfirmationAsync(
            notification.OrderId, notification.CustomerId, ct);
    }
}
```

Changes:
- Interface: `MediatR.INotificationHandler<T>` → `Nexum.Abstractions.INotificationHandler<T>`
- Add `[NotificationHandler]` attribute
- Method: `Handle(notification, cancellationToken)` → `HandleAsync(notification, ct)`
- Return type: `Task` → `ValueTask`

Nexum also adds publish strategies. Once your handlers are migrated, you can opt into parallel or fire-and-forget publishing:

```csharp
// Before (MediatR -- always sequential)
await mediator.Publish(new OrderCreatedEvent(orderId, customerId), ct);

// After (Nexum -- choose your strategy)
await notificationPublisher.PublishAsync(
    new OrderCreatedEvent(orderId, customerId),
    PublishStrategy.Parallel,
    ct);
```

See [Notifications](notifications.md) for full details on publish strategies.

### Step 2.5: Remove MediatR interfaces from request types

After all handlers for a given request type are migrated to Nexum, remove the MediatR interface from that type. Do this one type at a time.

```csharp
// Before (dual interface -- needed during migration)
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items)
    : MediatR.IRequest<Guid>, ICommand<Guid>;

// After (Nexum only)
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items)
    : ICommand<Guid>;
```

After removing `MediatR.IRequest<Guid>`, any code that calls `mediator.Send(new CreateOrderCommand(...))` will fail to compile -- which is intentional. Update those call sites to use `ICommandDispatcher.DispatchAsync()` instead:

```csharp
// Before
var orderId = await mediator.Send(new CreateOrderCommand(customerId, items), ct);

// After
var orderId = await commandDispatcher.DispatchAsync(new CreateOrderCommand(customerId, items), ct);
```

---

## Phase 3: Cleanup

Once all handlers and request types are migrated, remove the migration scaffolding.

### Step 3.1: Replace the DI registration

```csharp
// Before (migration compatibility)
builder.Services.AddNexumWithMediatRCompat(
    assemblies: typeof(Program).Assembly);

// After (Nexum only)
builder.Services.AddNexum(
    assemblies: typeof(Program).Assembly);
```

Or, if you installed `Nexum.Extensions.DependencyInjection` and use the Source Generator:

```csharp
builder.Services.AddNexum(); // Source Generator handles handler discovery
```

### Step 3.2: Remove the migration package

```bash
dotnet remove package Nexum.Migration.MediatR
```

### Step 3.3: Remove MediatR

```bash
dotnet remove package MediatR
dotnet remove package MediatR.Extensions.Microsoft.DependencyInjection
```

---

## Testing During Migration

- **Run existing tests after each handler migration.** The coexistence phase ensures both dispatchers return the same results.
- **Use the NEXUMM analyzers** to track progress. The IDE marks types still on MediatR-only interfaces:
  - `NEXUMM001` -- request type implements `MediatR.IRequest<T>` without a Nexum `ICommand<T>` or `IQuery<T>` interface.
  - `NEXUMM002` -- handler implements `MediatR.IRequestHandler<,>` without a Nexum handler interface.
  - `NEXUMM003` -- notification type implements `MediatR.INotification` without `Nexum.Abstractions.INotification`.
- **Cross-dispatch verification** -- during Phase 1, dispatching the same request through both `IMediator.Send()` and `ICommandDispatcher.DispatchAsync()` should yield identical results.

---

## Rollback Strategy

The migration is fully reversible at every step.

| Phase | How to Roll Back |
|-------|-----------------|
| Phase 1 (coexistence) | Remove the Nexum interfaces from request types; revert `AddNexumWithMediatRCompat()` to `AddMediatR()`. |
| Phase 2 (handler migration) | Re-add `MediatR.IRequest<T>` / `MediatR.INotification` to the type; restore the MediatR handler class; revert the Nexum handler. The adapter picks back up automatically. |
| Phase 3 (cleanup) | Re-add `Nexum.Migration.MediatR` package; restore `AddNexumWithMediatRCompat()`; re-add MediatR. |

No database migrations or persistent state changes are involved at any step.

---

## API Mapping Reference

| MediatR | Nexum | Notes |
|---------|-------|-------|
| `IRequest<T>` | `ICommand<T>` or `IQuery<T>` | Nexum separates reads from writes |
| `IRequestHandler<T, R>` | `ICommandHandler<T, R>` or `IQueryHandler<T, R>` | `HandleAsync()` returns `ValueTask<R>` |
| `IPipelineBehavior<T, R>` | `ICommandBehavior<T, R>` or `IQueryBehavior<T, R>` | Separate pipelines per type; `next(ct)` instead of `next()` |
| `MediatR.INotification` | `Nexum.Abstractions.INotification` | Same concept, different namespace |
| `MediatR.INotificationHandler<T>` | `Nexum.Abstractions.INotificationHandler<T>` | Adds publish strategies |
| `IStreamRequest<T>` | `IStreamQuery<T>` | Nexum has native `IAsyncEnumerable<T>` streaming |
| `IMediator.Send()` | `ICommandDispatcher.DispatchAsync()` or `IQueryDispatcher.DispatchAsync()` | Unified `DispatchAsync` naming |
| `IMediator.Publish()` | `INotificationPublisher.PublishAsync()` | Adds optional `PublishStrategy` parameter |
| `RequestHandlerDelegate<T>` | `CommandHandlerDelegate<T>` / `QueryHandlerDelegate<T>` | Nexum delegates accept `CancellationToken` |
| `Handle()` | `HandleAsync()` | Consistent `Async` suffix |
| `Task<T>` | `ValueTask<T>` | All handlers return `ValueTask<T>` |
| `AddMediatR()` | `AddNexum()` | Use `AddNexumWithMediatRCompat()` during transition |
| `[BehaviorOrder]` | `[BehaviorOrder(int)]` | Explicit pipeline ordering |

---

## FAQ

**Can I keep MediatR and Nexum running indefinitely in the same process?**

Yes. `AddNexumWithMediatRCompat()` is designed for long-running coexistence. There is no time limit. However, the adapters add a small overhead (one `Task`→`ValueTask` wrap per dispatch), so completing the migration eventually yields cleaner, faster code.

**What if the same request type has both a MediatR handler and a Nexum handler?**

The native Nexum handler takes priority. Adapters are registered with `TryAdd`, so a registered Nexum handler is never overwritten by an adapter.

**Do MediatR behaviors run when dispatching via Nexum?**

No. When a request is dispatched through `ICommandDispatcher` or `IQueryDispatcher`, it runs through the Nexum pipeline (Nexum behaviors only). The MediatR pipeline does not execute. During the transition, add equivalent Nexum behaviors before removing MediatR behaviors.

**Does the order of `AddNexumWithMediatRCompat()` registration matter?**

No. The method uses `TryAdd` for Nexum infrastructure, so calling it after an earlier `AddNexum()` call is safe -- the existing registrations are not overwritten.

**How do I handle `IVoidCommand` (commands with no return value)?**

MediatR's `IRequest` (non-generic) maps to `IVoidCommand : ICommand<Unit>` in Nexum. The handler returns `Unit.Value`:

```csharp
public async ValueTask<Unit> HandleAsync(DeleteOrderCommand command, CancellationToken ct)
{
    await _repo.DeleteAsync(command.OrderId, ct);
    return Unit.Value;
}
```

**What about streaming requests (`IStreamRequest<T>` in MediatR)?**

Nexum uses `IStreamQuery<T>` with native `IAsyncEnumerable<T>` support. There is no streaming adapter in `Nexum.Migration.MediatR` -- streaming handlers need to be rewritten directly as `IStreamQueryHandler<TQuery, TResult>`.

**The NEXUMM analyzers report a type I deliberately want to keep on MediatR. How do I suppress the diagnostic?**

Add a `#pragma warning disable NEXUMM001` comment above the type, or configure suppression in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.NEXUMM001.severity = none
```

**The migration analyzers stopped showing up after I removed `Nexum.Migration.MediatR`. Is that normal?**

Yes. NEXUMM001-NEXUMM003 are bundled in `Nexum.Migration.MediatR` and are only active while the package is referenced. Once migration is complete and the package is removed, the diagnostics disappear -- which is the expected end state.
