using Nexum.Abstractions;

namespace Nexum.Examples.Notifications.Notifications;

public sealed record PriceChangedNotification(string ProductName, decimal OldPrice, decimal NewPrice) : INotification;
