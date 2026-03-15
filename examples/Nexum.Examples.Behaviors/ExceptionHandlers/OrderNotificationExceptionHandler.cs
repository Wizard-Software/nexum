using Nexum.Abstractions;
using Nexum.Examples.Behaviors.Notifications;

namespace Nexum.Examples.Behaviors.ExceptionHandlers;

public sealed class OrderNotificationExceptionHandler : INotificationExceptionHandler<OrderPlacedNotification, Exception>
{
    public ValueTask HandleAsync(OrderPlacedNotification notification, Exception exception, CancellationToken ct = default)
    {
        Console.WriteLine($"  [EXCEPTION HANDLER] Notification failed for order {notification.OrderId}: {exception.Message}");
        return ValueTask.CompletedTask;
    }
}
