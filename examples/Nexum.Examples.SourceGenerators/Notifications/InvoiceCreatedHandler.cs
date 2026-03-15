using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Notifications;

// SG Tier 1: This handler is discovered via [NotificationHandler] and registered by
//            NexumHandlerRegistry.AddNexumHandlers() as an explicit ServiceDescriptor.
[NotificationHandler]
public sealed class InvoiceCreatedHandler : INotificationHandler<InvoiceCreatedNotification>
{
    public ValueTask HandleAsync(InvoiceCreatedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [Notification] Invoice created: {notification.InvoiceId:N} for {notification.Customer}");
        return ValueTask.CompletedTask;
    }
}
