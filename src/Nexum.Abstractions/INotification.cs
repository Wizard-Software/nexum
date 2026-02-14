namespace Nexum.Abstractions;

/// <summary>
/// Marker interface for domain event notifications.
/// Notifications are dispatched to multiple handlers and do not return a result.
/// </summary>
public interface INotification;
