# Nexum Documentation

Modern CQRS library for .NET 10 -- compile-time safe, zero-reflection, observable.

## Table of Contents

| Document | Description |
|----------|-------------|
| [Getting Started](getting-started.md) | Installation, requirements, and your first command |
| [Commands and Queries](commands-and-queries.md) | Core CQRS types, handlers, and dispatching |
| [Notifications](notifications.md) | Domain events and publish strategies |
| [Pipeline Behaviors](behaviors.md) | Cross-cutting concerns via the Russian doll model |
| [Exception Handlers](exception-handlers.md) | Structured exception handling outside the pipeline |
| [Configuration](configuration.md) | `NexumOptions`, DI registration, and handler lifetimes |
| [OpenTelemetry](opentelemetry.md) | Built-in distributed tracing and metrics |
| [Result Pattern](results.md) | Explicit error handling with `Result<T>` |
| [ASP.NET Core Integration](aspnetcore.md) | Middleware, endpoint mapping, and Problem Details |
| [Batching](batching.md) | Automatic query batching and deduplication |
| [Source Generators](source-generators.md) | Tiered compile-time acceleration |
| [API Reference](api-reference.md) | Complete public API surface |

## Package Overview

```
Nexum.Abstractions                  Zero-dependency contracts (ICommand, IQuery, INotification, etc.)
Nexum                               Runtime dispatchers and pipeline -- works standalone
Nexum.SourceGenerators              Optional compile-time accelerator (Roslyn analyzer)
Nexum.Extensions.DependencyInjection  IServiceCollection.AddNexum() extensions
Nexum.OpenTelemetry                 ActivitySource, metrics, System.Diagnostics integration
Nexum.Results                       Result<T, TError>, NexumError, IResultAdapter
Nexum.Results.FluentValidation      FluentValidation integration for Result pattern
Nexum.Extensions.AspNetCore         Middleware, endpoint routing, Problem Details
Nexum.Batching                      Automatic query batching with configurable windows
```

### Dependency Graph

```
Nexum.Abstractions (zero dependencies)
├── Nexum --> Abstractions (works STANDALONE without Source Generators)
│   ├── Nexum.OpenTelemetry --> Nexum
│   ├── Nexum.Extensions.DependencyInjection --> Nexum + SourceGenerators (optional)
│   └── Nexum.Extensions.AspNetCore --> Nexum
├── Nexum.Batching --> Abstractions + MSDI.Abstractions + Logging.Abstractions
├── Nexum.Results --> Abstractions
│   └── Nexum.Results.FluentValidation --> Results + FluentValidation + MSDI.Abstractions
└── Nexum.SourceGenerators (compile-only analyzer, OPTIONAL accelerator)
```
