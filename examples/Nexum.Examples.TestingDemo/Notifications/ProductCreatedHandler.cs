using Nexum.Abstractions;

namespace Nexum.Examples.TestingDemo.Notifications;

public sealed class ProductCreatedHandler : INotificationHandler<ProductCreatedNotification>
{
    public ValueTask HandleAsync(ProductCreatedNotification notification, CancellationToken ct = default)
    {
        // No-op in demo — notifications are collected by InMemoryNotificationCollector in tests
        Console.WriteLine($"  [ProductCreatedHandler] Product created: {notification.Id} — {notification.Name}");
        return ValueTask.CompletedTask;
    }
}
