using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Guards against excessive re-entrant dispatch depth using AsyncLocal tracking.
/// Thread-safe and async-flow-aware.
/// </summary>
internal static class DispatchDepthGuard
{
    private static readonly AsyncLocal<int> s_depth = new();

    /// <summary>
    /// Enters a new dispatch depth level, throwing if max depth is exceeded.
    /// </summary>
    /// <param name="maxDepth">Maximum allowed dispatch depth.</param>
    /// <returns>A disposable scope that decrements depth on disposal.</returns>
    /// <exception cref="NexumDispatchDepthExceededException">Thrown when current depth exceeds or equals maxDepth.</exception>
    /// <remarks>
    /// Usage: <c>using var _ = DispatchDepthGuard.Enter(options.MaxDispatchDepth);</c>
    /// <para>
    /// Returns a struct to avoid boxing — do not cast to IDisposable.
    /// </para>
    /// </remarks>
    public static DepthGuardScope Enter(int maxDepth)
    {
        if (s_depth.Value >= maxDepth)
        {
            throw new NexumDispatchDepthExceededException(maxDepth);
        }

        s_depth.Value++;
        return new DepthGuardScope();
    }

    /// <summary>
    /// Disposable scope that decrements dispatch depth on disposal.
    /// Returned as struct to avoid boxing allocation.
    /// </summary>
    internal readonly struct DepthGuardScope : IDisposable
    {
        /// <summary>
        /// Decrements the current dispatch depth unconditionally.
        /// </summary>
        public void Dispose()
        {
            s_depth.Value--;
        }
    }
}
