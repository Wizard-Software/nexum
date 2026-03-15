using Nexum.Abstractions;

namespace Nexum.Examples.BasicCqrs.Notifications;

public sealed class TaskCreatedAuditHandler : INotificationHandler<TaskCreatedNotification>
{
    public ValueTask HandleAsync(TaskCreatedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [AUDIT] Task #{notification.TaskId} recorded in audit trail");
        return ValueTask.CompletedTask;
    }
}
