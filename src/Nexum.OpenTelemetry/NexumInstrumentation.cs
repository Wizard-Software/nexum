using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Holds shared OpenTelemetry instruments (ActivitySource + Meter) for Nexum dispatchers.
/// Registered as singleton; disposed by the DI container.
/// </summary>
internal sealed class NexumInstrumentation : IDisposable
{
    private static readonly ConcurrentDictionary<Type, string> s_typeNameCache = new();

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivitySource"/> used to create tracing spans.
    /// </summary>
    public ActivitySource ActivitySource { get; }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.Metrics.Meter"/> used to create metric instruments.
    /// </summary>
    public Meter Meter { get; }

    /// <summary>
    /// Gets the counter tracking the number of dispatched commands and queries.
    /// Dimensions: <c>type</c>, <c>status</c>.
    /// </summary>
    public Counter<long> DispatchCount { get; }

    /// <summary>
    /// Gets the histogram tracking dispatch duration in milliseconds.
    /// Dimensions: <c>type</c>, <c>status</c>.
    /// </summary>
    public Histogram<double> DispatchDuration { get; }

    /// <summary>
    /// Gets the counter tracking the number of published notifications.
    /// Dimensions: <c>type</c>, <c>strategy</c>.
    /// </summary>
    public Counter<long> NotificationCount { get; }

    public NexumInstrumentation(NexumTelemetryOptions options)
    {
        ActivitySource = new ActivitySource(options.ActivitySourceName);
        Meter = new Meter(options.ActivitySourceName);

        DispatchCount = Meter.CreateCounter<long>(
            "nexum.dispatch.count",
            description: "Number of dispatched commands and queries");

        DispatchDuration = Meter.CreateHistogram<double>(
            "nexum.dispatch.duration",
            unit: "ms",
            description: "Duration of command and query dispatch in milliseconds");

        NotificationCount = Meter.CreateCounter<long>(
            "nexum.notification.count",
            description: "Number of published notifications");
    }

    /// <summary>
    /// Returns a cached type name to avoid string allocation on the hot path.
    /// </summary>
    public static string GetTypeName(Type type)
    {
        return s_typeNameCache.GetOrAdd(type, static t => t.Name);
    }

    /// <summary>
    /// Records dispatch metrics (count + duration) for command/query dispatchers.
    /// </summary>
    public void RecordDispatchMetrics(string typeName, string status, long startTimestamp)
    {
        if (DispatchCount.Enabled)
        {
            DispatchCount.Add(1, new TagList { { "type", typeName }, { "status", status } });
        }

        if (DispatchDuration.Enabled)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
            DispatchDuration.Record(elapsed.TotalMilliseconds,
                new TagList { { "type", typeName }, { "status", status } });
        }
    }

    /// <summary>
    /// Records notification count metric.
    /// </summary>
    public void RecordNotificationMetrics(string typeName, string strategy)
    {
        if (NotificationCount.Enabled)
        {
            NotificationCount.Add(1, new TagList { { "type", typeName }, { "strategy", strategy } });
        }
    }

    public void Dispose()
    {
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
