namespace Nexum.Abstractions;

/// <summary>
/// Thrown when the dispatch depth exceeds the configured maximum,
/// typically indicating infinite recursion in behaviors or handlers.
/// </summary>
public sealed class NexumDispatchDepthExceededException : InvalidOperationException
{
    /// <summary>Gets the maximum dispatch depth that was exceeded.</summary>
    public int MaxDepth { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NexumDispatchDepthExceededException"/>
    /// with the specified maximum depth.
    /// </summary>
    /// <param name="maxDepth">The maximum dispatch depth that was exceeded.</param>
    public NexumDispatchDepthExceededException(int maxDepth)
        : base($"Dispatch depth exceeded maximum of {maxDepth}. " +
               "This typically indicates infinite recursion in behaviors or handlers. " +
               "Configure MaxDispatchDepth in NexumOptions to adjust the limit.")
        => MaxDepth = maxDepth;
}
