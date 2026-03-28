# Nexum.OpenTelemetry

OpenTelemetry integration for [Nexum](https://github.com/asawicki/nexum). Automatic `Activity` tracing for all dispatches.

## Installation

```bash
dotnet add package Nexum.OpenTelemetry
```

## Usage

```csharp
builder.Services.AddNexum(options =>
{
    options.AddOpenTelemetry();
});
```

Every `DispatchAsync` / `PublishAsync` call automatically creates an `Activity` with command/query type, result status, and timing.

## License

MIT
