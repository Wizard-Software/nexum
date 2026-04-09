# Nexum Documentation

Welcome to the Nexum documentation. Nexum is a modern CQRS library for .NET 10 / C# 14 — a successor to MediatR built around strict command/query separation, `ValueTask` pipelines, optional compile-time source generators, and first-class OpenTelemetry support.

## Table of Contents

1. [Getting Started](getting-started.md) — installation, first command and handler, dispatch
2. [Commands and Queries](commands-and-queries.md) — core CQRS types, handlers, and dispatchers
3. [Notifications](notifications.md) — domain events and publish strategies
4. [Stream Queries](streams.md) — first-class `IAsyncEnumerable<T>` support
5. [Pipeline Behaviors](behaviors.md) — cross-cutting concerns via the Russian doll model
6. [Source Generators](source-generators.md) — tiered compile-time acceleration
7. [Dependency Injection](dependency-injection.md) — `AddNexum()`, lifetimes, manual registration
8. [ASP.NET Core Integration](aspnetcore-integration.md) — minimal APIs, middleware, Problem Details
9. [OpenTelemetry](opentelemetry.md) — distributed tracing and metrics
10. [Result Pattern](results.md) — explicit error handling with `Result<T, TError>`
11. [Batching](batching.md) — automatic query batching and deduplication
12. [Testing](testing.md) — `NexumTestHost`, fake dispatchers, behavior isolation
13. [Migration from MediatR](migration-from-mediatr.md) — gradual migration via adapters
14. [Architecture](architecture.md) — package graph, thread safety, dispatch internals

## Performance Highlights

- **Simple command dispatch** — **18.96 ns**, **0 B** allocated (Nexum SG). **2.1×** faster than MediatR.
- **Command + 3 behaviors** — **72.24 ns**, **192 B**. **1.7×** faster than MediatR, **3.8×** less memory.
- **Notifications (5 handlers, Sequential)** — **64.40 ns**, **32 B**. **2.2×** faster than MediatR, **28×** less memory.

Even without the Source Generator, the Runtime dispatcher is **1.5× faster** than MediatR with zero allocations for simple commands.

## Packages

| Package | Description |
|---|---|
| `Nexum.Abstractions` | Core interfaces (`ICommand`, `IQuery`, `INotification`, etc.). Zero dependencies. |
| `Nexum` | Runtime dispatchers, pipeline middleware. Works standalone. |
| `Nexum.SourceGenerators` | Optional Roslyn generators for compile-time handler registration and interceptors. |
| `Nexum.Extensions.DependencyInjection` | `IServiceCollection.AddNexum()` entry point. |
| `Nexum.Extensions.AspNetCore` | Middleware, endpoint routing, Problem Details integration. |
| `Nexum.OpenTelemetry` | `ActivitySource`, metrics, `System.Diagnostics` integration. |
| `Nexum.Results` | `Result<T, TError>`, `NexumError`, `IResultAdapter`. |
| `Nexum.Results.FluentValidation` | FluentValidation integration for the Result pattern. |
| `Nexum.Batching` | Automatic query batching with configurable windows. |
| `Nexum.Streaming` | Streaming notifications and SignalR integration. |
| `Nexum.Testing` | `NexumTestHost`, fake dispatchers, test helpers. |
| `Nexum.Migration.MediatR` | Adapters to run MediatR handlers inside Nexum during migration. |
| `Nexum.Migration.MediatR.Analyzers` | Roslyn analyzer suggesting MediatR → Nexum conversions. |
