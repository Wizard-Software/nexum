using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Decorator that adds OpenTelemetry tracing and metrics to notification publishing.
/// Wraps the inner <see cref="INotificationPublisher"/> with Activity spans and metric recording.
/// </summary>
internal sealed class TracingNotificationPublisher(
    INotificationPublisher inner,
    NexumTelemetryOptions options,
    NexumInstrumentation instrumentation) : INotificationPublisher
{
    public async ValueTask PublishAsync<TNotification>(
        TNotification notification,
        PublishStrategy? strategy = null,
        CancellationToken ct = default) where TNotification : INotification
    {
        if (!options.EnableTracing && !options.EnableMetrics)
        {
            await inner.PublishAsync(notification, strategy, ct).ConfigureAwait(false);
            return;
        }

        string notificationTypeName = NexumInstrumentation.GetTypeName(notification.GetType());
        string strategyName = strategy?.ToString() ?? "Default";
        using Activity? activity = options.EnableTracing
            ? instrumentation.ActivitySource.StartActivity(
                $"Nexum.Notification {notificationTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.notification.type", notificationTypeName);
        activity?.SetTag("nexum.notification.strategy", strategyName);

        try
        {
            await inner.PublishAsync(notification, strategy, ct).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            instrumentation.RecordNotificationMetrics(notificationTypeName, strategyName);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            instrumentation.RecordNotificationMetrics(notificationTypeName, strategyName);
            throw;
        }
    }
}
