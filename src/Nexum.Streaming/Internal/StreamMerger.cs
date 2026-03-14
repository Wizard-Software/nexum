using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Nexum.Streaming.Internal;

/// <summary>
/// Merges multiple <see cref="IAsyncEnumerable{T}"/> sources into a single interleaved stream
/// using a bounded channel.
/// </summary>
internal static class StreamMerger
{
    /// <summary>
    /// Merges multiple async enumerable sources into a single interleaved async enumerable.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="sources">The source streams to merge. Must not be null or empty.</param>
    /// <param name="channelCapacity">
    /// The capacity of the <see cref="System.Threading.Channels.Channel"/> used for merging.
    /// Controls backpressure: producers block when the channel is full.
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{T}"/> that yields items from all sources in arrival order.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each source is consumed on its own <see cref="Task"/>. Items are written to a shared
    /// bounded channel and yielded to the consumer in arrival order.
    /// </para>
    /// <para>
    /// If any source throws, all other sources are cancelled via a linked
    /// <see cref="CancellationTokenSource"/>, and the exception is propagated to the consumer.
    /// </para>
    /// <para>
    /// The channel uses <see cref="System.Threading.Channels.BoundedChannelFullMode.Wait"/> —
    /// producers block when the channel is at capacity, providing backpressure.
    /// </para>
    /// </remarks>
    internal static async IAsyncEnumerable<T> MergeAsync<T>(
        IAsyncEnumerable<T>[] sources,
        int channelCapacity,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true,
        });

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Launch one Task per source; all write to the shared channel
        Task[] producerTasks = new Task[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            producerTasks[i] = ProduceAsync(sources[i], channel.Writer, cts);
        }

        // When all producers complete, complete the channel (success or failure)
        _ = Task.WhenAll(producerTasks).ContinueWith(
            static (completed, state) =>
            {
                var writer = (ChannelWriter<T>)state!;
                writer.Complete(completed.Exception);
            },
            channel.Writer,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Yield items as they arrive
        await foreach (T item in channel.Reader.ReadAllAsync(cts.Token).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    private static async Task ProduceAsync<T>(
        IAsyncEnumerable<T> source,
        ChannelWriter<T> writer,
        CancellationTokenSource cts)
    {
        try
        {
            await foreach (T item in source.WithCancellation(cts.Token).ConfigureAwait(false))
            {
                await writer.WriteAsync(item, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // Cancellation is expected — propagate silently so WhenAll sees it cleanly
            throw;
        }
        catch (Exception)
        {
            // Cancel all other producers before re-throwing so WhenAll can aggregate
            await cts.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }
}
