# Dependency Injection

Nexum integrates with `Microsoft.Extensions.DependencyInjection` via the `Nexum.Extensions.DependencyInjection` package. The entry point is a single extension method: `AddNexum()`.

## `AddNexum` signatures

```csharp
// Source generator auto-discovery (when Nexum.SourceGenerators is installed)
services.AddNexum();

// Explicit assembly scanning (no source generator)
services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);

// With options
services.AddNexum(options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Sequential;
    options.MaxDispatchDepth = 16;
});

// Options + assemblies together
services.AddNexum(
    configure: opts => opts.MaxDispatchDepth = 32,
    assemblies: typeof(CreateOrderHandler).Assembly,
                typeof(SendOrderConfirmationEmail).Assembly);
```

Full declaration:

```csharp
public static IServiceCollection AddNexum(
    this IServiceCollection services,
    Action<NexumOptions>? configure = null,
    params Assembly[] assemblies);
```

## `NexumOptions`

| Option | Default | Description |
|--------|---------|-------------|
| `DefaultPublishStrategy` | `Sequential` | Strategy used when `PublishAsync` is called without an override. |
| `MaxDispatchDepth` | `16` | Maximum nested dispatch depth (re-entrancy guard). |
| `FireAndForgetChannelCapacity` | `1024` | Bounded channel size for the FireAndForget publish strategy. |

## Handler lifetimes

By default, handlers are registered as **Scoped** — one instance per DI scope (e.g., per HTTP request). Override per handler with `[HandlerLifetime]`:

```csharp
[CommandHandler]
[HandlerLifetime(NexumLifetime.Singleton)]
public sealed class WarmupHandler : ICommandHandler<WarmupCommand, Unit> { ... }

[CommandHandler]
[HandlerLifetime(NexumLifetime.Transient)]
public sealed class PerCallHandler : ICommandHandler<PerCallCommand, Unit> { ... }
```

`NexumLifetime` is Nexum's own enum (zero dependencies on `Microsoft.Extensions.DependencyInjection.Abstractions` at the abstraction layer). It maps to the standard MSDI `ServiceLifetime` inside the DI extension package.

## Dispatcher lifetime

All three dispatchers (`ICommandDispatcher`, `IQueryDispatcher`, `INotificationPublisher`) are registered as **Singleton**. They are thread-safe, their internal caches are `ConcurrentDictionary`, and they resolve scoped handlers from a `IServiceScopeFactory` on each dispatch.

## Manual registration

If you don't want to use either the Source Generator or assembly scanning, you can register handlers manually:

```csharp
services.AddNexumCore(); // registers dispatchers only
services.AddScoped<ICommandHandler<CreateOrderCommand, Guid>, CreateOrderHandler>();
services.AddScoped<IQueryHandler<GetOrderQuery, OrderDto?>, GetOrderQueryHandler>();
```

This path is useful for tightly controlled composition roots (e.g., AOT scenarios where you want full visibility into what gets registered).
