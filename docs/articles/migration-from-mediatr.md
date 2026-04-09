# Migration from MediatR

Nexum is designed as a gradual, drop-in evolution from MediatR. Both libraries can coexist in the same project during the transition — the `Nexum.Migration.MediatR` package provides adapters that let Nexum dispatchers execute MediatR handlers unchanged.

## Migration overview

| Concept | MediatR | Nexum |
|---------|---------|-------|
| Request type | `IRequest<T>` | `ICommand<T>` or `IQuery<T>` |
| Return type | `Task<T>` | `ValueTask<T>` |
| Handler method | `Handle` | `HandleAsync` |
| Dispatch method | `Send` | `DispatchAsync` |
| Pipeline | `IPipelineBehavior` (shared) | `ICommandBehavior` / `IQueryBehavior` (separate) |
| Notifications | `INotification` | `INotification` (same name, same idea) |
| Publish strategy | `INotificationPublisher` strategy class | `PublishStrategy` enum |

## Migration strategies

### Strategy A — Gradual conversion with adapters

Install both packages:

```bash
dotnet add package Nexum.Extensions.DependencyInjection
dotnet add package Nexum.Migration.MediatR
```

Register Nexum with MediatR compatibility:

```csharp
services.AddNexum();
services.AddNexumMediatRCompatibility(typeof(LegacyMediatRHandler).Assembly);
```

At runtime, `ICommandDispatcher.DispatchAsync(command)` will transparently route to either:
- A native Nexum handler (if one is registered), or
- A MediatR `IRequestHandler` wrapped in an adapter.

This lets you convert handlers one at a time without breaking consumers.

### Strategy B — Big-bang conversion

For small codebases, a single pass converting every `IRequest<T>` to `ICommand<T>`/`IQuery<T>` is often faster. The Nexum Roslyn analyzer (`Nexum.Migration.MediatR.Analyzers`) helps by emitting hints:

- `NEXUM-M001` — `IRequest<T>` should be replaced with `ICommand<T>` or `IQuery<T>` depending on whether the handler mutates state.
- `NEXUM-M002` — `Task<T>` handler return type should be `ValueTask<T>`.
- `NEXUM-M003` — `Handle` method should be renamed `HandleAsync`.
- `NEXUM-M004` — `Send` call should be replaced with `DispatchAsync` on the appropriate dispatcher.
- `NEXUM-M005` — shared `IPipelineBehavior<TRequest, TResponse>` should be split into `ICommandBehavior` and/or `IQueryBehavior`.

Each diagnostic ships with an accompanying code fix that performs the transformation automatically.

## Step by step

### 1. Install Nexum side-by-side with MediatR

```xml
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="Nexum" Version="1.*" />
<PackageReference Include="Nexum.Extensions.DependencyInjection" Version="1.*" />
<PackageReference Include="Nexum.Migration.MediatR" Version="1.*" />
<PackageReference Include="Nexum.Migration.MediatR.Analyzers" Version="1.*" />
```

### 2. Turn on the analyzer

The analyzer installs automatically. Set `NEXUM-M*` diagnostics to `warning` or `suggestion` in `.editorconfig` according to your tolerance for noise.

### 3. Convert handlers

Pick a bounded context and convert its requests:

```csharp
// Before (MediatR)
public record GetOrder(Guid Id) : IRequest<OrderDto?>;
public class GetOrderHandler : IRequestHandler<GetOrder, OrderDto?>
{
    public Task<OrderDto?> Handle(GetOrder request, CancellationToken ct) => ...;
}

// After (Nexum)
public record GetOrder(Guid Id) : IQuery<OrderDto?>;
[QueryHandler]
public sealed class GetOrderHandler : IQueryHandler<GetOrder, OrderDto?>
{
    public ValueTask<OrderDto?> HandleAsync(GetOrder query, CancellationToken ct) => ...;
}
```

### 4. Convert call sites

```csharp
// Before
var order = await mediator.Send(new GetOrder(id), ct);

// After
var order = await queries.DispatchAsync(new GetOrder(id), ct);
```

### 5. Remove MediatR

Once every handler has been converted and no code references `IMediator`, drop the MediatR package and `Nexum.Migration.MediatR`. The application now runs on Nexum exclusively.
