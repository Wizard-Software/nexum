using Nexum.Abstractions;

namespace Nexum.Examples.BasicCqrs.Notifications;

public sealed record TaskCreatedNotification(int TaskId, string Title) : INotification;
