# OpenTelemetry Integration

The `Nexum.OpenTelemetry` package adds automatic distributed tracing and metrics to every dispatch. No code changes are required in your handlers -- just add the package and configure it.

## Installation

```bash
dotnet add package Nexum.OpenTelemetry
```

## Setup

Register after `AddNexum()`:

```csharp
builder.Services.AddNexum();
builder.Services.AddNexumTelemetry(); // Uses defaults
```

With custom options:

```csharp
builder.Services.AddNexumTelemetry(options =>
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
    options.ActivitySourceName = "Nexum.Cqrs"; // Default
});
```

Wire up with your OpenTelemetry provider:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Nexum.Cqrs")  // Match ActivitySourceName
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("Nexum.Cqrs")   // Match ActivitySourceName
        .AddOtlpExporter());
```

## NexumTelemetryOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnableTracing` | `bool` | `true` | Create `Activity` spans for each dispatch |
| `EnableMetrics` | `bool` | `true` | Record counters and histograms |
| `ActivitySourceName` | `string` | `"Nexum.Cqrs"` | Name for `ActivitySource` and `Meter` |

## How It Works

`AddNexumTelemetry()` decorates the core dispatchers (`ICommandDispatcher`, `IQueryDispatcher`, `INotificationPublisher`) with tracing and metrics wrappers. Every dispatch automatically creates an `Activity` span with structured tags.

## Tracing

Each command, query, or notification dispatch creates an `Activity` with:

| Tag | Example Value | Description |
|-----|--------------|-------------|
| `nexum.type` | `CreateOrderCommand` | Short type name of the command/query |
| `nexum.kind` | `command` / `query` / `notification` | CQRS type kind |
| `nexum.status` | `ok` / `error` | Dispatch outcome |
| `otel.status_code` | `OK` / `ERROR` | Standard OpenTelemetry status |

For errors, the exception is recorded on the Activity with full stack trace.

### Example Trace

```
HTTP POST /orders  (ASP.NET Core)
  └── nexum.command CreateOrderCommand  (Nexum)
        ├── nexum.type = CreateOrderCommand
        ├── nexum.kind = command
        └── nexum.status = ok
```

## Metrics

Two instruments are exposed:

### `nexum.dispatch.count` (Counter)

Counts the number of dispatches.

| Dimension | Values |
|-----------|--------|
| `type` | Command/query type name |
| `status` | `ok`, `error` |

### `nexum.dispatch.duration` (Histogram)

Records dispatch duration in milliseconds.

| Dimension | Values |
|-----------|--------|
| `type` | Command/query type name |
| `status` | `ok`, `error` |

### `nexum.notification.count` (Counter)

Counts notification publishes.

| Dimension | Values |
|-----------|--------|
| `type` | Notification type name |
| `strategy` | `sequential`, `parallel`, `stop_on_exception`, `fire_and_forget` |

## NexumInstrumentation

The `NexumInstrumentation` class is registered as a singleton and provides direct access to the instrumentation primitives if needed:

```csharp
public sealed class NexumInstrumentation : IDisposable
{
    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    public Counter<long> DispatchCount { get; }
    public Histogram<double> DispatchDuration { get; }
    public Counter<long> NotificationCount { get; }
}
```

You typically don't need to interact with this class directly -- the tracing dispatchers handle everything automatically.

## Integration with Existing Telemetry

Nexum's `Activity` spans are children of the current ambient activity. If you're using ASP.NET Core with OpenTelemetry instrumentation, Nexum traces appear nested under HTTP request spans automatically -- no manual context propagation needed.
