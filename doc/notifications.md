# Notifications

Notifications model domain events -- things that have happened and that other parts of the system might be interested in. Unlike commands (one handler), notifications can have **multiple handlers**.

## Defining Notifications

```csharp
using Nexum.Abstractions;

public record OrderCreatedEvent(Guid OrderId, string CustomerId, DateTime CreatedAt) : INotification;

public record OrderShippedEvent(Guid OrderId, string TrackingNumber) : INotification;
```

## Notification Handlers

Implement `INotificationHandler<TNotification>`:

```csharp
[NotificationHandler]
public class SendOrderConfirmationEmail : INotificationHandler<OrderCreatedEvent>
{
    private readonly IEmailService _email;

    public SendOrderConfirmationEmail(IEmailService email) => _email = email;

    public async ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct)
    {
        await _email.SendOrderConfirmationAsync(notification.OrderId, notification.CustomerId, ct);
    }
}

[NotificationHandler]
public class UpdateAnalytics : INotificationHandler<OrderCreatedEvent>
{
    private readonly IAnalyticsService _analytics;

    public UpdateAnalytics(IAnalyticsService analytics) => _analytics = analytics;

    public async ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct)
    {
        await _analytics.TrackOrderCreatedAsync(notification.OrderId, ct);
    }
}
```

## Publishing Notifications

Inject `INotificationPublisher` and call `PublishAsync`:

```csharp
public class OrderService(
    ICommandDispatcher commandDispatcher,
    INotificationPublisher notificationPublisher)
{
    public async Task<Guid> CreateOrderAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = await commandDispatcher.DispatchAsync(command, ct);

        await notificationPublisher.PublishAsync(
            new OrderCreatedEvent(orderId, command.CustomerId, DateTime.UtcNow),
            ct: ct);

        return orderId;
    }
}
```

Signature:

```csharp
ValueTask PublishAsync<TNotification>(
    TNotification notification,
    PublishStrategy? strategy = null,
    CancellationToken ct = default)
    where TNotification : INotification;
```

## Publish Strategies

Nexum supports four strategies for executing notification handlers:

### Sequential (default)

Handlers execute one after another in registration order. All handlers run even if one throws. Exceptions are collected and re-thrown as `AggregateException` if multiple fail, or as the original exception if only one fails.

```csharp
await publisher.PublishAsync(notification, PublishStrategy.Sequential, ct);
```

### Parallel

All handlers execute concurrently via `Task.WhenAll`. Exceptions are collected and re-thrown after all handlers complete.

```csharp
await publisher.PublishAsync(notification, PublishStrategy.Parallel, ct);
```

### StopOnException

Handlers execute sequentially, but execution stops immediately when the first exception is thrown. Remaining handlers are skipped.

```csharp
await publisher.PublishAsync(notification, PublishStrategy.StopOnException, ct);
```

### FireAndForget

The notification is enqueued to a bounded channel and processed in the background by a `BackgroundService`. The `PublishAsync` call returns immediately without waiting for handlers to execute.

```csharp
await publisher.PublishAsync(notification, PublishStrategy.FireAndForget, ct);
```

Key characteristics:
- Each handler runs in its **own `IServiceScope`** (safe for scoped services like `DbContext`).
- Exceptions do **not** propagate to the caller.
- Exceptions are routed to `INotificationExceptionHandler<TNotification, TException>` if registered.
- The channel is bounded (default capacity: 1000). If full, the publish call blocks until space is available.
- On application shutdown, a drain timeout (default: 10s) allows in-flight notifications to complete.

## Default Strategy

Configure the default publish strategy in `NexumOptions`:

```csharp
builder.Services.AddNexum(options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Parallel;
});
```

When calling `PublishAsync` without a strategy, the default is used. Passing a strategy explicitly always overrides the default.

## FireAndForget Configuration

```csharp
builder.Services.AddNexum(options =>
{
    options.FireAndForgetTimeout = TimeSpan.FromSeconds(30);     // Per-handler timeout
    options.FireAndForgetChannelCapacity = 1000;                 // Bounded channel size
    options.FireAndForgetDrainTimeout = TimeSpan.FromSeconds(10); // Shutdown drain timeout
});
```

## Notification Exception Handlers

For `FireAndForget` notifications, exceptions cannot propagate to the caller. Register `INotificationExceptionHandler<TNotification, TException>` to handle them:

```csharp
public class OrderCreatedExceptionHandler
    : INotificationExceptionHandler<OrderCreatedEvent, Exception>
{
    private readonly ILogger<OrderCreatedExceptionHandler> _logger;

    public OrderCreatedExceptionHandler(ILogger<OrderCreatedExceptionHandler> logger)
        => _logger = logger;

    public ValueTask HandleAsync(
        OrderCreatedEvent notification, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception,
            "Failed to handle OrderCreatedEvent for order {OrderId}",
            notification.OrderId);
        return ValueTask.CompletedTask;
    }
}
```

See [Exception Handlers](exception-handlers.md) for full details.

## Strategy Comparison

| Strategy | Execution | Exception Handling | Use Case |
|----------|-----------|-------------------|----------|
| Sequential | One by one | All run, exceptions collected | Default, ordered side effects |
| Parallel | Concurrent | All run, exceptions collected | Independent handlers, performance |
| StopOnException | One by one | Stops on first error | Critical ordering, fail-fast |
| FireAndForget | Background | Via exception handlers | Non-critical, fire-and-forget events |
