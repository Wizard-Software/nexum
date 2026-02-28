# Configuration

## DI Registration

The main entry point is `AddNexum()` on `IServiceCollection`:

```csharp
using Nexum.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Mode A: Source Generator auto-discovery (recommended)
builder.Services.AddNexum();

// Mode B: Assembly scanning (without Source Generator)
builder.Services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);

// Mode C: With configuration
builder.Services.AddNexum(configure: options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Parallel;
    options.MaxDispatchDepth = 32;
});
```

Full signature:

```csharp
public static IServiceCollection AddNexum(
    this IServiceCollection services,
    Action<NexumOptions>? configure = null,
    params Assembly[] assemblies);
```

When `Nexum.SourceGenerators` is installed and no assemblies are provided, `AddNexum()` uses compile-time generated registrations (zero reflection). When assemblies are provided, runtime scanning is used.

## NexumOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DefaultPublishStrategy` | `PublishStrategy` | `Sequential` | Default strategy for `INotificationPublisher.PublishAsync` |
| `MaxDispatchDepth` | `int` | `16` | Maximum re-entrant dispatch depth before throwing |
| `FireAndForgetTimeout` | `TimeSpan` | `30s` | Per-handler timeout for FireAndForget notifications |
| `FireAndForgetChannelCapacity` | `int` | `1000` | Bounded channel size for FireAndForget queue |
| `FireAndForgetDrainTimeout` | `TimeSpan` | `10s` | Shutdown grace period for in-flight notifications |

Example:

```csharp
builder.Services.AddNexum(options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Parallel;
    options.MaxDispatchDepth = 32;
    options.FireAndForgetTimeout = TimeSpan.FromSeconds(60);
    options.FireAndForgetChannelCapacity = 5000;
    options.FireAndForgetDrainTimeout = TimeSpan.FromSeconds(30);
});
```

## Manual Handler Registration

Register handlers individually without Source Generator or assembly scanning:

```csharp
builder.Services.AddNexumHandler<
    ICommandHandler<CreateOrderCommand, Guid>,
    CreateOrderHandler>(NexumLifetime.Scoped);

builder.Services.AddNexumHandler<
    IQueryHandler<GetOrderQuery, OrderDto?>,
    GetOrderQueryHandler>();
```

## Behavior Registration

```csharp
// Open generic (applies to all commands)
builder.Services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>), order: 1);

// With lifetime override
builder.Services.AddNexumBehavior(
    typeof(CachingQueryBehavior<,>),
    order: 5,
    lifetime: NexumLifetime.Scoped);

// Closed generic (specific type only)
builder.Services.AddNexumBehavior(
    typeof(SpecialValidation<CreateOrderCommand, Guid>));
```

## Exception Handler Registration

```csharp
builder.Services.AddNexumExceptionHandler<OrderExceptionHandler>();
builder.Services.AddNexumExceptionHandler<GlobalCommandExceptionHandler<ICommand>>(
    NexumLifetime.Singleton);
```

## Result Adapter Registration

```csharp
builder.Services.AddNexumResultAdapter<NexumResultAdapter<OrderDto>>();
```

## Handler Lifetimes

| Component | Default Lifetime | Override |
|-----------|-----------------|----------|
| Command/Query/Stream handlers | **Scoped** | `[HandlerLifetime]` attribute or registration parameter |
| Notification handlers | **Scoped** | `[HandlerLifetime]` attribute or registration parameter |
| Behaviors | **Transient** | Registration parameter |
| Exception handlers | **Transient** | Registration parameter |
| Dispatchers | **Singleton** | Not configurable (thread-safe by design) |

### Using the HandlerLifetime Attribute

Override the default lifetime per-handler:

```csharp
[CommandHandler]
[HandlerLifetime(NexumLifetime.Singleton)]
public class CachedConfigHandler : ICommandHandler<GetConfigCommand, ConfigDto>
{
    // This handler is registered as Singleton
}
```

`NexumLifetime` values: `Transient`, `Scoped`, `Singleton`.

## Registered Services

After calling `AddNexum()`, the following services are available for injection:

| Service | Lifetime | Description |
|---------|----------|-------------|
| `ICommandDispatcher` | Singleton | Dispatches commands |
| `IQueryDispatcher` | Singleton | Dispatches queries and stream queries |
| `INotificationPublisher` | Singleton | Publishes notifications |
| `NexumOptions` | Singleton | Configuration (read-only after startup) |
| All discovered handlers | Scoped (default) | One per DI scope |
| `NotificationBackgroundService` | Hosted Service | Processes FireAndForget notifications |

## OpenTelemetry Registration

Call after `AddNexum()`:

```csharp
builder.Services.AddNexum();
builder.Services.AddNexumTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.ActivitySourceName = "Nexum.Cqrs";
});
```

See [OpenTelemetry](opentelemetry.md) for details.

## ASP.NET Core Registration

```csharp
builder.Services.AddNexum();
builder.Services.AddNexumAspNetCore(
    configureProblemDetails: pd =>
    {
        pd.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    },
    configureEndpoints: ep =>
    {
        ep.SuccessStatusCode = 200;
        ep.FailureStatusCode = 400;
    });
```

See [ASP.NET Core Integration](aspnetcore.md) for details.
