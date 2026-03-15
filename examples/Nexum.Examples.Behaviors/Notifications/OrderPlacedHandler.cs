using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Notifications;

public sealed class OrderPlacedHandler : INotificationHandler<OrderPlacedNotification>
{
    public ValueTask HandleAsync(OrderPlacedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [Handler] Processing order {notification.OrderId}");
        throw new InvalidOperationException("Simulated notification error");
    }
}
