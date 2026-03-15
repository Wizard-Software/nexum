using Nexum.Abstractions;

namespace Nexum.Examples.BasicCqrs.Notifications;

public sealed class TaskCreatedLogHandler : INotificationHandler<TaskCreatedNotification>
{
    public ValueTask HandleAsync(TaskCreatedNotification notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [LOG] Task created: #{notification.TaskId} {notification.Title}");
        return ValueTask.CompletedTask;
    }
}
