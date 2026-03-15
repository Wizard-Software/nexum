using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexum.Abstractions;
using Nexum.Streaming.Internal;

namespace Nexum.Streaming;

/// <summary>
/// Default implementation of <see cref="IStreamNotificationPublisher"/> that resolves all registered
/// <see cref="IStreamNotificationHandler{TNotification, TItem}"/> instances and merges their streams.
/// </summary>
/// <remarks>
/// <para>
/// This publisher is thread-safe and designed to be registered as a singleton.
/// </para>
/// <para>
/// The dispatch flow:
/// </para>
/// <list type="number">
/// <item>Resolve all <see cref="IStreamNotificationHandler{TNotification,TItem}"/> registrations from DI.</item>
/// <item>If 0 handlers — return an empty <see cref="IAsyncEnumerable{T}"/> (no allocation).</item>
/// <item>If 1 handler — return its stream directly (fast path, no channel overhead).</item>
/// <item>If N handlers — merge streams via <see cref="StreamMerger.MergeAsync{T}"/> using a <c>BoundedChannel</c>.</item>
/// </list>
/// <para>
/// NativeAOT note: this class resolves open-generic handler types from DI at runtime, which may
/// not be compatible with NativeAOT trimming. Use the Source Generator path for trimming-safe dispatch.
/// </para>
/// </remarks>
[RequiresUnreferencedCode(
    "StreamNotificationPublisher resolves open-generic IStreamNotificationHandler<,> from DI at runtime. " +
    "Use the Source Generator path for NativeAOT / trim-safe dispatch.")]
[RequiresDynamicCode(
    "StreamNotificationPublisher resolves open-generic handler types from DI at runtime. " +
    "Use the Source Generator path for NativeAOT-safe dispatch.")]
public sealed class StreamNotificationPublisher : IStreamNotificationPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NexumStreamingOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamNotificationPublisher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve stream notification handlers.</param>
    /// <param name="options">The streaming options (e.g., merge channel capacity).</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="serviceProvider"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public StreamNotificationPublisher(
        IServiceProvider serviceProvider,
        IOptions<NexumStreamingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(options);
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TItem> StreamAsync<TNotification, TItem>(
        TNotification notification,
        CancellationToken ct = default)
        where TNotification : IStreamNotification<TItem>
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Resolve all handlers for this notification type
        IEnumerable<IStreamNotificationHandler<TNotification, TItem>> handlers =
            _serviceProvider.GetServices<IStreamNotificationHandler<TNotification, TItem>>();

        // Materialise into a list once — we need Count and random access
        var handlerList = handlers as IReadOnlyList<IStreamNotificationHandler<TNotification, TItem>>
            ?? [.. handlers];

        return handlerList.Count switch
        {
            // No handlers registered — return empty stream (no allocation)
            0 => AsyncEnumerable.Empty<TItem>(),

            // Single handler — forward its stream directly (no channel overhead)
            1 => handlerList[0].HandleAsync(notification, ct),

            // Multiple handlers — interleave via bounded channel
            _ => MergeHandlerStreams(handlerList, notification, ct),
        };
    }

    private IAsyncEnumerable<TItem> MergeHandlerStreams<TNotification, TItem>(
        IReadOnlyList<IStreamNotificationHandler<TNotification, TItem>> handlers,
        TNotification notification,
        CancellationToken ct)
        where TNotification : IStreamNotification<TItem>
    {
        IAsyncEnumerable<TItem>[] sources = new IAsyncEnumerable<TItem>[handlers.Count];
        for (int i = 0; i < handlers.Count; i++)
        {
            sources[i] = handlers[i].HandleAsync(notification, ct);
        }

        return StreamMerger.MergeAsync(sources, _options.MergeChannelCapacity, ct);
    }
}
