namespace Nexum.Streaming;

/// <summary>
/// Configuration options for Nexum.Streaming.
/// </summary>
public sealed class NexumStreamingOptions
{
    /// <summary>
    /// Gets or sets the capacity of the bounded channel used to merge streams from multiple handlers.
    /// </summary>
    /// <remarks>
    /// When multiple handlers are registered for a streaming notification, their output streams
    /// are merged via a <c>BoundedChannel</c>. This capacity controls backpressure — producers block
    /// when the channel is full (<see cref="System.Threading.Channels.BoundedChannelFullMode.Wait"/>).
    /// A higher capacity reduces blocking at the cost of memory. Default: 1024.
    /// </remarks>
    public int MergeChannelCapacity { get; set; } = 1024;
}
