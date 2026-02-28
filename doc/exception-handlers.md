# Exception Handlers

Nexum provides structured exception handling **outside** the pipeline via dedicated exception handler interfaces. Exception handlers are side-effect only -- the dispatcher always re-throws after invoking them.

This is different from pipeline behaviors, which can catch and transform exceptions. Exception handlers are for observing exceptions (logging, metrics, alerts) without altering the flow.

## Exception Handler Interfaces

### Command Exception Handler

```csharp
public interface ICommandExceptionHandler<in TCommand, in TException>
    where TCommand : ICommand
    where TException : Exception
{
    ValueTask HandleAsync(TCommand command, TException exception, CancellationToken ct = default);
}
```

### Query Exception Handler

```csharp
public interface IQueryExceptionHandler<in TQuery, in TException>
    where TQuery : IQuery
    where TException : Exception
{
    ValueTask HandleAsync(TQuery query, TException exception, CancellationToken ct = default);
}
```

### Notification Exception Handler

```csharp
public interface INotificationExceptionHandler<in TNotification, in TException>
    where TNotification : INotification
    where TException : Exception
{
    ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default);
}
```

## Usage

### Logging Failed Commands

```csharp
public class LoggingCommandExceptionHandler<TCommand>
    : ICommandExceptionHandler<TCommand, Exception>
    where TCommand : ICommand
{
    private readonly ILogger<LoggingCommandExceptionHandler<TCommand>> _logger;

    public LoggingCommandExceptionHandler(
        ILogger<LoggingCommandExceptionHandler<TCommand>> logger)
        => _logger = logger;

    public ValueTask HandleAsync(TCommand command, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception,
            "Command {CommandType} failed: {Message}",
            typeof(TCommand).Name,
            exception.Message);
        return ValueTask.CompletedTask;
    }
}
```

### Specific Exception Types

```csharp
public class ConcurrencyExceptionHandler
    : ICommandExceptionHandler<CreateOrderCommand, DbUpdateConcurrencyException>
{
    private readonly IMetricsService _metrics;

    public ConcurrencyExceptionHandler(IMetricsService metrics) => _metrics = metrics;

    public ValueTask HandleAsync(
        CreateOrderCommand command,
        DbUpdateConcurrencyException exception,
        CancellationToken ct)
    {
        _metrics.IncrementCounter("order.concurrency_conflict");
        return ValueTask.CompletedTask;
    }
}
```

### FireAndForget Notification Errors

This is the primary use case for `INotificationExceptionHandler`. When using `PublishStrategy.FireAndForget`, exceptions cannot propagate to the caller -- they are routed to exception handlers instead.

```csharp
public class OrderEventExceptionHandler
    : INotificationExceptionHandler<OrderCreatedEvent, Exception>
{
    private readonly ILogger<OrderEventExceptionHandler> _logger;
    private readonly IAlertService _alerts;

    public OrderEventExceptionHandler(
        ILogger<OrderEventExceptionHandler> logger, IAlertService alerts)
    {
        _logger = logger;
        _alerts = alerts;
    }

    public async ValueTask HandleAsync(
        OrderCreatedEvent notification, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception,
            "Failed to process OrderCreatedEvent for order {OrderId}",
            notification.OrderId);

        await _alerts.SendAlertAsync(
            $"OrderCreatedEvent handler failed for {notification.OrderId}", ct);
    }
}
```

## Resolution Order

Exception handlers are resolved **most-specific-first** based on two axes:

1. **Command/Query type** -- a handler for `CreateOrderCommand` is preferred over one for `ICommand`
2. **Exception type** -- a handler for `DbUpdateConcurrencyException` is preferred over one for `Exception`

If a specific handler exists for both the exact command type and the exact exception type, it runs first. Then broader handlers run.

## Registration

```csharp
builder.Services.AddNexumExceptionHandler<LoggingCommandExceptionHandler<ICommand>>();
builder.Services.AddNexumExceptionHandler<ConcurrencyExceptionHandler>();
builder.Services.AddNexumExceptionHandler<OrderEventExceptionHandler>();
```

Exception handlers are registered as **Transient** by default.

## Key Behavior

- Exception handlers are **side-effect only** -- the original exception is always re-thrown after all handlers execute.
- For `FireAndForget` notifications, exceptions are routed to handlers instead of being re-thrown (there is no caller to throw to).
- If an exception handler itself throws, the original exception takes precedence.
- Stream queries (`StreamAsync`) do **not** have exception handlers -- exceptions propagate through the `IAsyncEnumerable<T>`.
