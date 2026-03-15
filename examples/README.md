# Nexum Examples

Runnable console applications demonstrating Nexum features.

## Examples

| Project | Description | Key Concepts |
|---------|-------------|--------------|
| `Nexum.Examples.BasicCqrs` | Basic CQRS patterns | `ICommand<T>`, `IVoidCommand`, `IQuery<T>`, `IStreamQuery<T>`, `INotification`, `AddNexum()` |
| `Nexum.Examples.Behaviors` | Pipeline behaviors and exception handling | `ICommandBehavior`, `IQueryBehavior`, `IStreamQueryBehavior`, `[BehaviorOrder]`, `ICommandExceptionHandler`, `INotificationExceptionHandler` |
| `Nexum.Examples.Notifications` | Notification publish strategies | `PublishStrategy.Sequential`, `.Parallel`, `.StopOnException`, `.FireAndForget` |
| `Nexum.Examples.WebApi` | ASP.NET Core Minimal API integration | `MapNexumCommand<T,R>`, `MapNexumQuery<T,R>`, `UseNexum()`, `AddNexumAspNetCore()`, OpenAPI |
| `Nexum.Examples.ResultsValidation` | Result pattern + FluentValidation | `Result<T>`, `NexumError`, `Map`/`Bind`/`GetValueOrDefault`, `AddNexumFluentValidation()`, `ValidationNexumError` |
| `Nexum.Examples.Observability` | OpenTelemetry tracing and metrics | `AddNexumTelemetry()`, `ActivitySource`, console exporter, trace spans |
| `Nexum.Examples.Batching` | DataLoader / N+1 prevention | `IBatchQueryHandler<T,K,R>`, `AddNexumBatching()`, batch window, concurrent dispatch |
| `Nexum.Examples.Streaming` | SignalR + Server-Sent Events streaming | `IStreamNotification<T>`, `NexumStreamHubBase`, `MapNexumStream<T,R>`, `AddNexumStreaming()`, SSE |
| `Nexum.Examples.MigrationFromMediatR` | Gradual migration from MediatR | `AddNexumWithMediatRCompat()`, dual-interface pattern, `IRequest<T>` + `ICommand<T>`, adapter bridge |
| `Nexum.Examples.Advanced` | Advanced Runtime features | `MaxDispatchDepth`, `NexumDispatchDepthExceededException`, `[HandlerLifetime]`, polymorphic resolution, `NexumOptions` |
| `Nexum.Examples.TestingDemo` | Test helpers and fake dispatchers | `NexumTestHost`, `FakeCommandDispatcher`, `FakeQueryDispatcher`, `InMemoryNotificationCollector`, `ShouldHaveDispatched<T>()`, `ShouldHavePublished<T>()` |
| `Nexum.Examples.SourceGenerators` | Source Generator Tier 1-3 | `[CommandHandler]`, `[QueryHandler]`, compiled pipeline delegates, `[InterceptsLocation]`, `[HandlerLifetime]` |
| `Nexum.Examples.NexumEndpoints` | Auto-generated endpoints | `[NexumEndpoint]`, `MapNexumEndpoints()`, `WithNexumResultMapping()`, `NexumResultEndpointFilter`, OpenAPI |

## Running

```bash
# From the repository root:

# Console examples:
dotnet run --project examples/Nexum.Examples.BasicCqrs
dotnet run --project examples/Nexum.Examples.Behaviors
dotnet run --project examples/Nexum.Examples.Notifications
dotnet run --project examples/Nexum.Examples.ResultsValidation
dotnet run --project examples/Nexum.Examples.Batching
dotnet run --project examples/Nexum.Examples.MigrationFromMediatR
dotnet run --project examples/Nexum.Examples.Advanced
dotnet run --project examples/Nexum.Examples.SourceGenerators

# Web examples (open browser after starting):
dotnet run --project examples/Nexum.Examples.WebApi              # http://localhost:5000/openapi/v1.json
dotnet run --project examples/Nexum.Examples.Observability       # http://localhost:5000 (check console for traces)
dotnet run --project examples/Nexum.Examples.Streaming           # http://localhost:5000 (SSE demo page)
dotnet run --project examples/Nexum.Examples.NexumEndpoints      # http://localhost:5000/openapi/v1.json

# TestingDemo (xUnit — run via dotnet test):
dotnet test examples/Nexum.Examples.TestingDemo
```

## Prerequisites

- .NET 10 SDK
- No additional setup required — examples use in-memory stores
