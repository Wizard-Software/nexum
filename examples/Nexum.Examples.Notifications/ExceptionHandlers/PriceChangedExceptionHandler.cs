using Nexum.Abstractions;
using Nexum.Examples.Notifications.Notifications;

namespace Nexum.Examples.Notifications.ExceptionHandlers;

public sealed class PriceChangedExceptionHandler : INotificationExceptionHandler<PriceChangedNotification, Exception>
{
    public ValueTask HandleAsync(PriceChangedNotification notification, Exception exception, CancellationToken ct = default)
    {
        Console.WriteLine($"  [EXCEPTION HANDLER] Error for {notification.ProductName}: {exception.Message}");
        return ValueTask.CompletedTask;
    }
}
