# Source Generators

The `Nexum.SourceGenerators` package provides optional compile-time acceleration using Roslyn Source Generators. Nexum works fully without it (runtime assembly scanning), but the Source Generator eliminates reflection, enables NativeAOT, and progressively improves performance.

## Installation

```bash
dotnet add package Nexum.SourceGenerators
```

The package is a compile-only analyzer -- it produces no runtime assembly. It generates code at build time that is compiled into your project.

## Tiered Architecture

The Source Generator uses three tiers, each building on the previous:

### Tier 1: DI Registration + Compile-Time Diagnostics

**What it does:**
- Generates `AddNexum()` registrations at compile time (zero reflection).
- Emits compile-time diagnostics (NEXUM001--NEXUM004) for common mistakes.
- Uses `ForAttributeWithMetadataName` for fast handler discovery.

**Diagnostics:**

| Code | Severity | Description |
|------|----------|-------------|
| NEXUM001 | Error | Handler class must not be abstract |
| NEXUM002 | Error | Handler class must have a public constructor |
| NEXUM003 | Warning | Handler implements interface but is missing marker attribute |
| NEXUM004 | Error | Duplicate handler for the same command/query type |

### Tier 2: Compiled Pipeline Delegates

**What it does:**
- Generates monomorphized dispatch delegates for each handler type.
- Bypasses the runtime pipeline builder entirely.
- Eliminates generic virtual dispatch overhead.

With Tier 2, dispatching `CreateOrderCommand` calls a generated delegate that directly invokes `CreateOrderHandler.HandleAsync` with the correct types -- no runtime generic resolution needed.

### Tier 3: Interceptors

**What it does:**
- Uses Roslyn Interceptors to replace `DispatchAsync` call sites at compile time.
- Eliminates virtual dispatch entirely -- the call is replaced with a direct method call.
- Provides the fastest possible dispatch path.

Interceptors are stable since .NET 9.0.2xx SDK.

## Performance Comparison

| Tier | Mean (simple command) | Allocated | vs Runtime |
|------|----------------------:|----------:|-----------:|
| Tier 3 -- Interceptor | 16.55 ns | 0 B | 1.52x faster |
| Tier 2 -- Compiled Pipeline | 19.04 ns | 0 B | 1.32x faster |
| Tier 1 -- Runtime | 25.19 ns | 0 B | baseline |

All tiers achieve zero allocations for simple command dispatch.

## Marker Attributes

The Source Generator discovers handlers via marker attributes:

```csharp
[CommandHandler]       // ICommandHandler<TCommand, TResult>
[QueryHandler]         // IQueryHandler<TQuery, TResult>
[StreamQueryHandler]   // IStreamQueryHandler<TQuery, TResult>
[NotificationHandler]  // INotificationHandler<TNotification>
```

These attributes are defined in `Nexum.Abstractions` and have no effect at runtime -- they are metadata for the Source Generator.

## Without Source Generator

If you don't install `Nexum.SourceGenerators`, Nexum falls back to runtime behavior:

```csharp
// Explicitly provide assemblies for scanning
builder.Services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);
```

Runtime mode uses:
- Reflection-based assembly scanning for handler discovery.
- `ConcurrentDictionary<Type, Lazy<Type?>>` for thread-safe handler caching.
- Polymorphic dispatch through generic interfaces.

Runtime mode is fully functional and already faster than MediatR (1.5x for simple commands). The Source Generator is an optimization, not a requirement.

## NativeAOT Support

With the Source Generator (Tier 1+), Nexum is fully compatible with NativeAOT compilation. No runtime reflection is used -- all handler registration and pipeline wiring happens at compile time.

Without the Source Generator, NativeAOT is not supported due to assembly scanning.

## Generated Code

The Source Generator produces:

1. **`NexumHandlerRegistry`** -- a static class with handler type mappings, consumed by `AddNexum()`.
2. **`NexumPipelineRegistry`** (Tier 2) -- compiled pipeline delegates for each handler.
3. **Interceptor methods** (Tier 3) -- `[InterceptsLocation]`-attributed methods that replace `DispatchAsync` calls.

All generated code is placed in the `Nexum.Generated` namespace and is `internal`.
