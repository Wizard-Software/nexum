# Streaming

The `Nexum.Streaming` package provides streaming notifications, SignalR integration, and Server-Sent Events (SSE) endpoints for real-time CQRS scenarios.

## Installation

```bash
dotnet add package Nexum.Streaming
```

## When to Use

Streaming is useful when:
- You need to push a sequence of items to clients as they become available (live order updates, sensor readings, log tailing)
- Multiple independent data sources contribute to a single logical stream and their output should be interleaved
- You want to expose real-time streams over SignalR or SSE without writing transport boilerplate
- Clients subscribe to data that changes over time rather than requesting a single response

For one-shot domain events with multiple side-effect handlers, use `INotification` and `INotificationPublisher` instead (see [Notifications](notifications.md)).

## Streaming Notifications

### IStreamNotification\<TItem\>

A streaming notification represents an intent to open a stream. Unlike `INotification` (a one-shot event), publishing a streaming notification returns a merged `IAsyncEnumerable<TItem>` from all registered handlers.

```csharp
using Nexum.Abstractions;

public record OrderUpdatesNotification(Guid OrderId) : IStreamNotification<OrderUpdate>;

public record OrderUpdate(Guid OrderId, string Status, DateTimeOffset OccurredAt);
```

The `TItem` type parameter is covariant, so a handler returning `IAsyncEnumerable<OrderUpdate>` satisfies `IStreamNotification<object>` at the publisher call site.

### IStreamNotificationHandler

Implement `IStreamNotificationHandler<TNotification, TItem>` to produce a stream for a given notification. The handler returns `IAsyncEnumerable<TItem>` — yield items as they become available:

```csharp
[StreamNotificationHandler]
public class DatabaseOrderUpdatesHandler
    : IStreamNotificationHandler<OrderUpdatesNotification, OrderUpdate>
{
    private readonly IOrderRepository _repo;

    public DatabaseOrderUpdatesHandler(IOrderRepository repo) => _repo = repo;

    public async IAsyncEnumerable<OrderUpdate> HandleAsync(
        OrderUpdatesNotification notification,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var update in _repo.WatchOrderAsync(notification.OrderId, ct))
        {
            yield return update;
        }
    }
}
```

The interface:

```csharp
public interface IStreamNotificationHandler<in TNotification, TItem>
    where TNotification : IStreamNotification<TItem>
{
    IAsyncEnumerable<TItem> HandleAsync(TNotification notification, CancellationToken ct = default);
}
```

Mark handlers with `[StreamNotificationHandler]` for automatic DI registration via the Source Generator (optional — manual registration also works).

### Publishing (IStreamNotificationPublisher)

Inject `IStreamNotificationPublisher` and call `StreamAsync`:

```csharp
public class OrderStreamService(IStreamNotificationPublisher publisher)
{
    public IAsyncEnumerable<OrderUpdate> WatchOrderAsync(Guid orderId, CancellationToken ct)
        => publisher.StreamAsync<OrderUpdatesNotification, OrderUpdate>(
               new OrderUpdatesNotification(orderId), ct);
}
```

Signature:

```csharp
IAsyncEnumerable<TItem> StreamAsync<TNotification, TItem>(
    TNotification notification, CancellationToken ct = default)
    where TNotification : IStreamNotification<TItem>;
```

Consume the returned stream with `await foreach`:

```csharp
await foreach (var update in publisher.StreamAsync<OrderUpdatesNotification, OrderUpdate>(notification, ct))
{
    Console.WriteLine($"Order {update.OrderId}: {update.Status}");
}
```

### Multiple Handlers (Stream Merging)

When multiple handlers are registered for the same `IStreamNotification<TItem>`, their streams are merged. Items from all handlers are interleaved as they arrive -- the merged stream completes only when every handler stream has completed.

```
Handler A: ──a1────a2────────a3──►
Handler B: ──────b1────b2────────►
Merged:    ──a1──b1──a2──b2──a3──►
```

The merge order within a tick is non-deterministic -- handlers run concurrently and items are delivered as they become available. Registration order does not determine delivery order.

A single-handler case is optimised: the publisher forwards the handler's stream directly without any channel overhead.

## SignalR Integration

SignalR hub methods must be public and non-generic. This prevents a single generic hub method from covering all stream types. Nexum solves this with a dual-path design:

- **Runtime path** -- inherit from `NexumStreamHubBase` and write concrete hub methods by hand.
- **Source Generator path** -- annotate a partial hub class with `[NexumStreamHub]` and let the generator emit the hub methods.

### NexumStreamHubBase (Runtime Path)

Inherit from `NexumStreamHubBase` and call the protected helpers from your typed hub methods:

```csharp
public sealed class OrderStreamHub : NexumStreamHubBase
{
    public OrderStreamHub(
        IQueryDispatcher queryDispatcher,
        IStreamNotificationPublisher notificationPublisher)
        : base(queryDispatcher, notificationPublisher) { }

    // Stream query -- reads an existing data stream
    public IAsyncEnumerable<OrderUpdate> StreamOrderUpdates(
        GetOrderUpdatesQuery query, CancellationToken ct)
        => StreamQueryAsync<GetOrderUpdatesQuery, OrderUpdate>(query, ct);

    // Stream notification -- merges all registered handlers
    public IAsyncEnumerable<OrderUpdate> WatchOrder(
        OrderUpdatesNotification notification, CancellationToken ct)
        => StreamNotificationAsync<OrderUpdatesNotification, OrderUpdate>(notification, ct);
}
```

Available protected helpers:

| Method | Description |
|--------|-------------|
| `StreamQueryAsync<TQuery, TResult>(query, ct)` | Dispatches a stream query via `IQueryDispatcher.StreamAsync` |
| `StreamNotificationAsync<TNotification, TItem>(notification, ct)` | Publishes a stream notification and returns the merged item stream |

### Source Generator Hub Methods (Zero Boilerplate)

With the `Nexum.SourceGenerators` package, annotate a `partial` hub class with `[NexumStreamHub]`. The generator discovers all registered `IStreamQueryHandler` and `IStreamNotificationHandler` types and emits a typed public hub method for each:

```csharp
[NexumStreamHub]
public partial class OrderStreamHub : NexumStreamHubBase
{
    public OrderStreamHub(
        IQueryDispatcher queryDispatcher,
        IStreamNotificationPublisher notificationPublisher)
        : base(queryDispatcher, notificationPublisher) { }

    // Source Generator emits:
    // public IAsyncEnumerable<OrderUpdate> StreamGetOrderUpdates(GetOrderUpdatesQuery q, CancellationToken ct)
    //     => StreamQueryAsync<GetOrderUpdatesQuery, OrderUpdate>(q, ct);
    //
    // public IAsyncEnumerable<OrderUpdate> StreamOrderUpdates(OrderUpdatesNotification n, CancellationToken ct)
    //     => StreamNotificationAsync<OrderUpdatesNotification, OrderUpdate>(n, ct);
}
```

The generator derives hub method names by stripping the `Query` or `Notification` suffix and prepending `Stream`.

### Registration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register Nexum core services
builder.Services.AddNexum();

// Register streaming publisher
builder.Services.AddNexumStreaming();

// Register SignalR and map the hub
builder.Services.AddNexumSignalR();

var app = builder.Build();

app.MapHub<OrderStreamHub>("/hubs/orders");

app.Run();
```

`AddNexumSignalR()` is a thin wrapper around `AddSignalR()` that provides a discoverable entry point. Call `AddNexumStreaming()` separately to register the `IStreamNotificationPublisher`.

## SSE Endpoints

### MapNexumStream

Map a stream query directly to a Server-Sent Events endpoint using .NET 10 native SSE support:

```csharp
app.MapNexumStream<GetOrderUpdatesQuery, OrderUpdate>("/api/orders/stream");
```

This is equivalent to:

```csharp
app.MapGet("/api/orders/stream", (
    [AsParameters] GetOrderUpdatesQuery query,
    IQueryDispatcher dispatcher,
    CancellationToken ct) =>
{
    IAsyncEnumerable<OrderUpdate> stream = dispatcher.StreamAsync<OrderUpdate>(query, ct);
    return TypedResults.ServerSentEvents(stream);
})
.Produces<OrderUpdate>(200, "text/event-stream");
```

The framework sets `Content-Type: text/event-stream`, handles SSE framing, and serializes each stream element as a JSON `data:` field. The HTTP connection's `CancellationToken` is propagated to the stream handler -- closing the browser tab or dropping the network connection terminates the query.

Query properties are bound from route values and query string parameters via `[AsParameters]`:

```csharp
public record GetOrderUpdatesQuery(Guid OrderId, int? MaxUpdates = null) : IStreamQuery<OrderUpdate>;
```

```
GET /api/orders/stream?orderId=abc&maxUpdates=50
```

`MapNexumStream` returns `RouteHandlerBuilder`, so you can chain standard endpoint configuration:

```csharp
app.MapNexumStream<GetOrderUpdatesQuery, OrderUpdate>("/api/orders/stream")
    .RequireAuthorization()
    .WithTags("Orders")
    .WithName("StreamOrderUpdates");
```

### OpenAPI

`MapNexumStream` automatically registers OpenAPI metadata:

- `Produces<TResult>(200, "text/event-stream")` -- documents the SSE response type
- `.WithName(...)` -- derives the operation name from the query type (e.g., `GetOrderUpdatesQuery` becomes `GetOrderUpdates`)

> **Known limitation:** `[AsParameters]` binding may mark all query properties as required in OpenAPI metadata, even nullable or default-valued ones (ASP.NET Core issue #52881). Use explicit `[FromQuery]` attributes or a custom `BindAsync` method as a workaround.

## Configuration

### NexumStreamingOptions

Configure streaming options in `AddNexumStreaming`:

```csharp
builder.Services.AddNexumStreaming(options =>
{
    options.MergeChannelCapacity = 2048;
});
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MergeChannelCapacity` | `int` | `1024` | Capacity of the bounded channel used to merge streams from multiple handlers. Producers block when full. Higher values reduce blocking at the cost of memory. |

`MergeChannelCapacity` only applies when two or more handlers are registered for the same streaming notification. Single-handler and zero-handler cases bypass the channel entirely.

## Package Dependencies

```
Nexum.Streaming --> Nexum.Abstractions
                --> Microsoft.AspNetCore.SignalR (optional, for hub support)
                --> Microsoft.AspNetCore.Http (for SSE endpoint mapping)
```

`Nexum.Streaming` has no dependency on `Nexum` (the runtime dispatcher package) -- it depends only on `Nexum.Abstractions` for the `IStreamNotification<T>`, `IStreamNotificationHandler<,>`, and `IStreamQuery<T>` contracts. Register `AddNexum()` separately to wire up the core dispatchers.
