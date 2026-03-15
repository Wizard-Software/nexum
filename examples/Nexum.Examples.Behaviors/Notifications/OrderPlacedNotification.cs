using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Notifications;

public sealed record OrderPlacedNotification(string OrderId) : INotification;
