using Nexum.Abstractions;

namespace Nexum.Examples.Notifications.Notifications;

public sealed class PriceChangedSlowHandler : INotificationHandler<PriceChangedNotification>
{
    public async ValueTask HandleAsync(PriceChangedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [SLOW] Starting slow processing for {notification.ProductName}...");
        await Task.Delay(500, ct).ConfigureAwait(false);
        Console.WriteLine($"  [SLOW] Completed slow processing for {notification.ProductName}");
    }
}
