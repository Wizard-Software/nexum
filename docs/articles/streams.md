# Stream Queries

Stream queries return a sequence of values over time via `IAsyncEnumerable<T>`. They are separate from regular queries — dispatched through `IQueryDispatcher.StreamAsync()` and processed by dedicated `IStreamQueryBehavior<TQuery, TResult>` pipeline behaviors.

## Defining a stream query

```csharp
public record TailOrderEventsQuery(Guid OrderId) : IStreamQuery<OrderEventDto>;
```

## Handler

```csharp
[StreamQueryHandler]
public sealed class TailOrderEventsHandler
    : IStreamQueryHandler<TailOrderEventsQuery, OrderEventDto>
{
    private readonly IOrderEventLog _log;
    public TailOrderEventsHandler(IOrderEventLog log) => _log = log;

    public async IAsyncEnumerable<OrderEventDto> HandleAsync(
        TailOrderEventsQuery query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var evt in _log.TailAsync(query.OrderId, ct))
        {
            yield return evt.ToDto();
        }
    }
}
```

## Dispatching

```csharp
app.MapGet("/orders/{id:guid}/events", (
    Guid id,
    IQueryDispatcher dispatcher,
    CancellationToken ct) =>
{
    var stream = dispatcher.StreamAsync(new TailOrderEventsQuery(id), ct);
    return TypedResults.Stream(stream);
});
```

Notice that stream dispatch uses `StreamAsync`, not `DispatchAsync` — the distinction is intentional. Stream semantics (backpressure, cancellation, partial results) are fundamentally different from a single `ValueTask<T>` return, so Nexum uses a separate method name to prevent accidental misuse.

## Stream pipeline behaviors

`IStreamQueryBehavior<TQuery, TResult>` lets you wrap a stream handler with cross-cutting concerns that understand async enumerables:

```csharp
public sealed class LoggingStreamBehavior<TQuery, TResult>
    : IStreamQueryBehavior<TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    private readonly ILogger<LoggingStreamBehavior<TQuery, TResult>> _log;
    public LoggingStreamBehavior(ILogger<LoggingStreamBehavior<TQuery, TResult>> log) => _log = log;

    public async IAsyncEnumerable<TResult> HandleAsync(
        TQuery query,
        StreamQueryHandlerDelegate<TResult> next,
        [EnumeratorCancellation] CancellationToken ct)
    {
        _log.LogInformation("Streaming {Query}", typeof(TQuery).Name);
        var count = 0;
        await foreach (var item in next().WithCancellation(ct))
        {
            count++;
            yield return item;
        }
        _log.LogInformation("Streamed {Count} items from {Query}", count, typeof(TQuery).Name);
    }
}
```

## Backpressure

Because the return is an `IAsyncEnumerable<T>`, the consumer drives iteration speed. If the consumer pauses, the handler pauses — there is no internal buffering beyond what the handler itself introduces. This makes stream queries naturally backpressure-friendly for HTTP streaming and SignalR.
