using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Carries a notification through the FireAndForget bounded channel.
/// </summary>
/// <remarks>
/// Boxing <see cref="INotification"/> is a conscious trade-off (ADR-004).
/// <see cref="NotificationType"/> is needed for reflective <c>MakeGenericType</c> in
/// <see cref="NotificationBackgroundService"/>.
/// <see cref="CancellationToken"/> is intentionally NOT captured — background processing
/// uses its own linked token with <c>FireAndForgetTimeout</c>.
/// </remarks>
internal readonly record struct NotificationEnvelope(
    INotification Notification,
    Type NotificationType,
    ExecutionContext? CapturedContext);
