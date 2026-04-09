# Notifications

Notifications are Nexum's model for domain events — fire-and-remember messages that can have zero, one, or many independent handlers. They are dispatched via `INotificationPublisher.PublishAsync()`.

## Defining a notification

```csharp
public record OrderCreated(Guid OrderId, string CustomerId, DateTimeOffset At) : INotification;
```

## Handlers

```csharp
[NotificationHandler]
public sealed class SendOrderConfirmationEmail : INotificationHandler<OrderCreated>
{
    private readonly IEmailService _email;
    public SendOrderConfirmationEmail(IEmailService email) => _email = email;

    public ValueTask HandleAsync(OrderCreated notification, CancellationToken ct)
        => _email.SendOrderConfirmationAsync(notification.OrderId, ct);
}
```

Any number of handlers can subscribe to the same notification type. All of them will run according to the configured publish strategy.

## Publish strategies

Nexum ships with four strategies, selectable globally via `NexumOptions.DefaultPublishStrategy` or per-call:

### Sequential (default)
Handlers run one after another on the caller's task. First exception propagates immediately; subsequent handlers do not run.

### Parallel
All handlers start concurrently via `Task.WhenAll`. Exceptions are aggregated and thrown together.

### StopOnException
Same as Sequential but explicit about the semantics — useful when you want to make the stop-on-first-failure behavior visible in code review.

### FireAndForget
The notification is queued to a bounded channel and processed on a background `BackgroundService`. `PublishAsync` returns immediately. Each handler runs in its own `IServiceScope`. **Exceptions do not propagate to the caller** — they must be routed to `INotificationExceptionHandler<TNotification, TException>` or they will be lost.

```csharp
services.AddNexum(options =>
{
    options.DefaultPublishStrategy = PublishStrategy.Parallel;
});
```

Per-call override:

```csharp
await publisher.PublishAsync(new OrderCreated(...), PublishStrategy.FireAndForget, ct);
```

## Exception handlers for notifications

Because FireAndForget swallows exceptions from the caller's perspective, Nexum provides a dedicated sink:

```csharp
public sealed class OrderCreatedExceptionHandler
    : INotificationExceptionHandler<OrderCreated, Exception>
{
    public ValueTask HandleAsync(OrderCreated notification, Exception exception, CancellationToken ct)
    {
        // log, metrics, dead-letter queue, etc.
        return ValueTask.CompletedTask;
    }
}
```

Exception handlers are resolved most-specific-first — both by notification type and exception type.

## When to use which strategy

| Strategy | Use when |
|----------|----------|
| Sequential | You need deterministic order and fast failure (default). |
| Parallel | Handlers are independent and you want max throughput. |
| StopOnException | Same as Sequential but you want the intent explicit. |
| FireAndForget | HTTP request path — do not block the user on side effects. |
