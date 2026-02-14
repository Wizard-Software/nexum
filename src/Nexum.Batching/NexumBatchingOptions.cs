namespace Nexum.Batching;

/// <summary>
/// Configuration options for Nexum batching / dataloader behavior.
/// </summary>
public sealed class NexumBatchingOptions
{
    /// <summary>
    /// Time window for collecting queries before flushing the batch.
    /// Default: 10ms. Must be in range [1ms, 30s].
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when value is less than 1ms or greater than 30s.
    /// </exception>
    public TimeSpan BatchWindow
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.FromMilliseconds(1), nameof(value));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromSeconds(30), nameof(value));
            field = value;
        }
    } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Maximum number of queries in a single batch. Flush triggered immediately when reached.
    /// Default: 100. Must be in range [1, 10_000].
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when value is less than or equal to 0 or greater than 10,000.
    /// </exception>
    public int MaxBatchSize
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0, nameof(value));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 10_000, nameof(value));
            field = value;
        }
    } = 100;

    /// <summary>
    /// Timeout for draining pending batches during application shutdown.
    /// Default: 5s. Must be greater than <see cref="TimeSpan.Zero"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when value is less than or equal to <see cref="TimeSpan.Zero"/>.
    /// </exception>
    public TimeSpan DrainTimeout
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero, nameof(value));
            field = value;
        }
    } = TimeSpan.FromSeconds(5);
}
