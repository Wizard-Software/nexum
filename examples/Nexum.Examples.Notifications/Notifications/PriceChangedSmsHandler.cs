using Nexum.Abstractions;

namespace Nexum.Examples.Notifications.Notifications;

public sealed class PriceChangedSmsHandler : INotificationHandler<PriceChangedNotification>
{
    public async ValueTask HandleAsync(PriceChangedNotification notification, CancellationToken ct = default)
    {
        if (notification.NewPrice < 0)
        {
            throw new InvalidOperationException("Negative price not allowed");
        }

        Console.WriteLine($"  [SMS] Price changed for {notification.ProductName}: {notification.OldPrice:C} -> {notification.NewPrice:C}");
        await Task.Delay(100, ct).ConfigureAwait(false);
    }
}
