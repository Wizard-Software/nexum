# OpenTelemetry

The `Nexum.OpenTelemetry` package adds built-in distributed tracing and metrics. Every command, query, stream, and notification dispatch automatically creates an `Activity` with structured tags — no code changes required in your handlers.

## Setup

```bash
dotnet add package Nexum.OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

```csharp
using Nexum.OpenTelemetry;

builder.Services.AddNexum();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddNexumInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddNexumInstrumentation()
        .AddOtlpExporter());
```

## Activity tags

Every dispatch emits an `Activity` on the `Nexum` `ActivitySource` with these tags:

| Tag | Example | Description |
|-----|---------|-------------|
| `nexum.kind` | `command`, `query`, `stream`, `notification` | Kind of operation. |
| `nexum.type` | `CreateOrderCommand` | CLR name of the message type. |
| `nexum.namespace` | `Orders.Commands` | Namespace of the message type. |
| `nexum.handler` | `CreateOrderHandler` | Resolved handler type. |
| `nexum.result` | `success`, `failure` | Outcome of the dispatch. |
| `nexum.depth` | `1` | Nested dispatch depth at the time of the call. |
| `exception.type` | `ValidationException` | Set on failure. |
| `exception.message` | `OrderId cannot be empty` | Set on failure. |

## Metrics

The package exposes a `Meter` named `Nexum` with:

- `nexum.dispatch.duration` (histogram, ms) — tagged by kind, type, result.
- `nexum.dispatch.count` (counter) — tagged by kind, type, result.
- `nexum.notifications.fire_and_forget.queue_depth` (observable gauge) — current channel depth.
- `nexum.notifications.fire_and_forget.dropped` (counter) — messages dropped because the channel was full.

## Correlation across dispatches

Because Nexum uses the standard `System.Diagnostics.Activity` API, parent/child correlation works automatically. A command dispatched from inside another handler becomes a child Activity, nested under the outer span.

## Zero-overhead when disabled

If no `ActivityListener` is subscribed to the `Nexum` source, `Activity.Current` returns `null` and the generated start/stop code short-circuits. The cost of "having OpenTelemetry installed but no exporter configured" is a handful of nanoseconds per dispatch.
