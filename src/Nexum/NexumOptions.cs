using Nexum.Abstractions;

namespace Nexum;

/// <summary>
/// Runtime configuration options for the Nexum CQRS library.
/// </summary>
public sealed class NexumOptions
{
    /// <summary>
    /// Gets or sets the default publish strategy used when calling
    /// <c>INotificationPublisher.PublishAsync(notification, strategy: null)</c>.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="PublishStrategy.Sequential"/>.
    /// </remarks>
    public PublishStrategy DefaultPublishStrategy { get; set; } = PublishStrategy.Sequential;

    /// <summary>
    /// Gets or sets the timeout for each notification when using <see cref="PublishStrategy.FireAndForget"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to 30 seconds. Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan FireAndForgetTimeout
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(value));
            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of queued notifications for <see cref="PublishStrategy.FireAndForget"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to 1000. Must be greater than 0.
    /// Uses a bounded channel internally.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to 0.
    /// </exception>
    public int FireAndForgetChannelCapacity
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(value));
            field = value;
        }
    } = 1000;

    /// <summary>
    /// Gets or sets the timeout for draining the FireAndForget channel during application shutdown.
    /// </summary>
    /// <remarks>
    /// Defaults to 10 seconds. Must be greater than <see cref="TimeSpan.Zero"/>.
    /// The background service will attempt to process remaining notifications up to this timeout.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan FireAndForgetDrainTimeout
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(value));
            field = value;
        }
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the maximum allowed re-entrant dispatch depth to prevent stack overflow
    /// in recursive command/query scenarios.
    /// </summary>
    /// <remarks>
    /// Defaults to 16. Must be greater than 0.
    /// Tracked via <see cref="AsyncLocal{T}"/> per async execution context.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than or equal to 0.
    /// </exception>
    public int MaxDispatchDepth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(value));
            field = value;
        }
    } = 16;

    /// <summary>
    /// Gets the behavior ordering overrides set by <c>AddNexumBehavior(order:)</c> registrations.
    /// </summary>
    /// <remarks>
    /// Maps behavior concrete types to their explicit order values.
    /// Visible to Nexum.Extensions.DependencyInjection via <c>InternalsVisibleTo</c>.
    /// </remarks>
    internal Dictionary<Type, int> BehaviorOrderOverrides { get; } = [];

    /// <summary>
    /// Gets or sets the type of the generated NexumPipelineRegistry (Tier 2).
    /// Set by AddNexum() when a NexumPipelineRegistry is detected in scanned assemblies.
    /// </summary>
    internal Type? PipelineRegistryType { get; set; }
}
