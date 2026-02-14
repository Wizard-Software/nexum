namespace Nexum.OpenTelemetry;

/// <summary>
/// Configuration options for Nexum OpenTelemetry instrumentation.
/// </summary>
public sealed class NexumTelemetryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether distributed tracing is enabled.
    /// When <c>true</c>, dispatchers create <see cref="System.Diagnostics.Activity"/> spans.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableTracing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether metrics collection is enabled.
    /// When <c>true</c>, dispatchers record counters and histograms.
    /// Default is <c>true</c>.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets the name used for the <see cref="System.Diagnostics.ActivitySource"/>
    /// and <see cref="System.Diagnostics.Metrics.Meter"/>.
    /// Default is <c>"Nexum.Cqrs"</c>.
    /// </summary>
    public string ActivitySourceName { get; set; } = "Nexum.Cqrs";
}
