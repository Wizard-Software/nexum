# Architecture

This article covers the internals of Nexum: package layout, dispatch internals, thread safety guarantees, and the re-entrancy guard.

## Package dependency graph

```
Nexum.Abstractions (zero dependencies — referenced by domain layers)
├── Nexum → Abstractions (runtime; works STANDALONE without source generators)
│   ├── Nexum.OpenTelemetry → Nexum
│   ├── Nexum.Extensions.DependencyInjection → Nexum + SourceGenerators (optional)
│   └── Nexum.Extensions.AspNetCore → Nexum
├── Nexum.Batching → Abstractions + MSDI.Abstractions + Logging.Abstractions
├── Nexum.Migration.MediatR → Abstractions + MediatR (migration adapters, temporary)
├── Nexum.Testing → Nexum + Abstractions + MSDI + Logging.Abstractions
├── Nexum.Results → Abstractions
│   └── Nexum.Results.FluentValidation → Results + FluentValidation + MSDI.Abstractions
├── Nexum.Streaming → Abstractions + Nexum + FrameworkReference Microsoft.AspNetCore.App
└── Nexum.SourceGenerators (compile-only analyzer, OPTIONAL accelerator)
```

- `Nexum.Abstractions` has **zero dependencies** — it is safe to reference from pure domain projects without dragging MSDI, logging, or telemetry into the domain layer.
- `Nexum` is the runtime. It builds on top of abstractions and works without any source generator.
- `Nexum.SourceGenerators` is a compile-time analyzer — it produces no runtime assembly and is marked `DevelopmentDependency`.

## Polymorphic handler resolution

Each dispatcher caches handler types in a `ConcurrentDictionary<Type, Lazy<Type?>>`:

- Key — the closed generic command/query type.
- Value — `Lazy<Type?>` — the resolved handler type, discovered once and then served from cache.

Polymorphic lookup walks the inheritance chain: if `DeleteCommandBase` has a handler and `DeleteOrderCommand : DeleteCommandBase` does not, the base handler is found and cached against the derived key.

## Re-entrant dispatch protection

Nexum guards against runaway re-entrant dispatch with an `AsyncLocal<int>` depth counter. Each call to `DispatchAsync`:

1. Increments the depth counter.
2. If the counter exceeds `NexumOptions.MaxDispatchDepth` (default 16), throws `DispatchDepthExceededException`.
3. Decrements the counter in a `finally` block.

Because the counter is `AsyncLocal<int>`, it correctly tracks depth across `await` boundaries and does not leak between independent requests.

## Thread safety

- `ICommandDispatcher`, `IQueryDispatcher`, `INotificationPublisher` — thread-safe, registered as **Singleton**.
- Handler caches — `ConcurrentDictionary`, safe for concurrent reads and writes.
- Handler instances — resolved per-dispatch from a `IServiceScopeFactory`, so scoped handlers get a fresh scope on every call.
- `NexumOptions` — immutable after `AddNexum` has been called.

## `ConfigureAwait(false)` everywhere

Nexum is a library, not an application. Every internal `await` uses `.ConfigureAwait(false)` so that the dispatcher never captures or restores a synchronization context. This is important for ASP.NET Core (which has no sync context) but also for any framework that does — Nexum never forces back-to-context hops on the caller.

## FireAndForget notification pipeline

The FireAndForget publish strategy is backed by a bounded `Channel<NotificationWorkItem>` and a `BackgroundService`:

1. `PublishAsync` writes a work item to the channel and returns immediately.
2. The background service dequeues items, creates a fresh `IServiceScope`, resolves handlers inside it, and runs them.
3. Exceptions are routed to the registered `INotificationExceptionHandler<TNotification, TException>` chain.
4. The channel has a configurable capacity (`FireAndForgetChannelCapacity`, default 1024). When full, new publishes drop the oldest item and increment the `nexum.notifications.fire_and_forget.dropped` counter.

## Why ValueTask?

Nexum uses `ValueTask<T>` instead of `Task<T>` because most command and query handlers complete synchronously (cache hits, trivial transformations, precomputed results). `ValueTask<T>` avoids the `Task` heap allocation and the state-machine box on those paths. On genuinely asynchronous paths, `ValueTask<T>` wraps a `Task<T>` internally at the cost of a tiny struct copy — negligible compared to the async overhead itself.

## Further reading

- [Source Generators](source-generators.md) — tiered compile-time acceleration.
- [Dependency Injection](dependency-injection.md) — `AddNexum()` and handler lifetimes.
- [Pipeline Behaviors](behaviors.md) — the Russian doll model and behavior ordering.
