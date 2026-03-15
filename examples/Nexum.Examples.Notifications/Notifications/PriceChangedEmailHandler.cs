using Nexum.Abstractions;

namespace Nexum.Examples.Notifications.Notifications;

public sealed class PriceChangedEmailHandler : INotificationHandler<PriceChangedNotification>
{
    public async ValueTask HandleAsync(PriceChangedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [EMAIL] Price changed for {notification.ProductName}: {notification.OldPrice:C} -> {notification.NewPrice:C}");
        await Task.Delay(100, ct).ConfigureAwait(false);
    }
}
