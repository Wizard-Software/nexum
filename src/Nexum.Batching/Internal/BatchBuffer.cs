using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexum.Abstractions;

namespace Nexum.Batching.Internal;

/// <summary>
/// Thread-safe buffer that collects queries and flushes them as a batch.
/// Uses atomic swap for lock-free hot path and SemaphoreSlim for async-safe flush.
/// </summary>
internal sealed class BatchBuffer<TQuery, TKey, TResult> : IAsyncDisposable
    where TQuery : IQuery<TResult>
    where TKey : notnull
{
    private volatile ConcurrentDictionary<TKey, BatchEntry> _pending = new();
    private readonly Timer _timer;
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private readonly NexumBatchingOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly AsyncServiceScope _keyExtractorScope;
    private readonly IBatchQueryHandler<TQuery, TKey, TResult> _keyExtractorHandler;
    private int _count;
    private volatile bool _disposed;

    public BatchBuffer(
        NexumBatchingOptions options,
        IServiceScopeFactory scopeFactory,
        ILogger logger)
    {
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Dedicated scope for GetKey — synchronous, pure function, lives with buffer
        _keyExtractorScope = scopeFactory.CreateAsyncScope();
        _keyExtractorHandler = _keyExtractorScope.ServiceProvider
            .GetRequiredService<IBatchQueryHandler<TQuery, TKey, TResult>>();

        _timer = new Timer(
            static state => _ = ((BatchBuffer<TQuery, TKey, TResult>)state!).FlushAsync(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public ValueTask<TResult> EnqueueAsync(TQuery query, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        TKey key = _keyExtractorHandler.GetKey(query);

        var entry = _pending.GetOrAdd(key, static (_, args) =>
        {
            var tcs = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            return new BatchEntry(args.Query, tcs);
        }, new EnqueueArgs(query));

        if (ct.CanBeCanceled)
        {
            ct.Register(static state =>
            {
                var e = (BatchEntry)state!;
                e.Tcs.TrySetCanceled();
            }, entry);
        }

        int count = Interlocked.Increment(ref _count);

        if (count >= _options.MaxBatchSize)
        {
            _ = FlushAsync();
        }
        else if (count == 1)
        {
            _timer.Change(_options.BatchWindow, Timeout.InfiniteTimeSpan);
        }

        return new ValueTask<TResult>(entry.Tcs.Task);
    }

    internal async Task FlushAsync()
    {
        if (_disposed)
        {
            return;
        }

        ConcurrentDictionary<TKey, BatchEntry> toProcess =
            Interlocked.Exchange(ref _pending, new ConcurrentDictionary<TKey, BatchEntry>());
        Interlocked.Exchange(ref _count, 0);

        if (toProcess.IsEmpty)
        {
            return;
        }

        await _flushLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            // Filter out cancelled entries before creating scope
            List<KeyValuePair<TKey, BatchEntry>> activeEntries = toProcess
                .Where(kvp => !kvp.Value.Tcs.Task.IsCanceled)
                .ToList();

            if (activeEntries.Count == 0)
            {
                return;
            }

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider
                .GetRequiredService<IBatchQueryHandler<TQuery, TKey, TResult>>();

            List<TQuery> queries = activeEntries.Select(kvp => kvp.Value.Query).ToList();

            IReadOnlyDictionary<TKey, TResult> results = await handler
                .HandleAsync(queries, CancellationToken.None)
                .ConfigureAwait(false);

            foreach (KeyValuePair<TKey, BatchEntry> kvp in toProcess)
            {
                if (kvp.Value.Tcs.Task.IsCanceled)
                {
                    continue;
                }

                if (results.TryGetValue(kvp.Key, out TResult? result))
                {
                    kvp.Value.Tcs.TrySetResult(result);
                }
                else
                {
                    kvp.Value.Tcs.TrySetException(
                        new KeyNotFoundException(
                            $"Batch handler did not return a result for key '{kvp.Key}' " +
                            $"(query type: {typeof(TQuery).Name})."));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing batch for {QueryType}.", typeof(TQuery).Name);

            foreach (BatchEntry entry in toProcess.Values)
            {
                entry.Tcs.TrySetException(ex);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Stop timer first to prevent new flushes from timer callback
        await _timer.DisposeAsync().ConfigureAwait(false);

        // Drain remaining entries using pre-existing handler (scope factory may be disposed)
        if (!_pending.IsEmpty)
        {
            try
            {
                await DrainAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error draining batch buffer for {QueryType} during shutdown.",
                    typeof(TQuery).Name);
            }
        }

        _disposed = true;
        _flushLock.Dispose();
        await _keyExtractorScope.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Flushes remaining entries during shutdown using the pre-existing key extractor handler.
    /// Unlike <see cref="FlushAsync"/>, does not create a new scope (scope factory may be disposed).
    /// </summary>
    private async Task DrainAsync()
    {
        ConcurrentDictionary<TKey, BatchEntry> toProcess =
            Interlocked.Exchange(ref _pending, new ConcurrentDictionary<TKey, BatchEntry>());
        Interlocked.Exchange(ref _count, 0);

        if (toProcess.IsEmpty)
        {
            return;
        }

        await _flushLock.WaitAsync().ConfigureAwait(false);
        try
        {
            List<KeyValuePair<TKey, BatchEntry>> activeEntries = toProcess
                .Where(kvp => !kvp.Value.Tcs.Task.IsCanceled)
                .ToList();

            if (activeEntries.Count == 0)
            {
                return;
            }

            List<TQuery> queries = activeEntries.Select(kvp => kvp.Value.Query).ToList();

            // Use pre-existing handler from key extractor scope (shutdown-safe)
            IReadOnlyDictionary<TKey, TResult> results = await _keyExtractorHandler
                .HandleAsync(queries, CancellationToken.None)
                .ConfigureAwait(false);

            foreach (KeyValuePair<TKey, BatchEntry> kvp in toProcess)
            {
                if (kvp.Value.Tcs.Task.IsCanceled)
                {
                    continue;
                }

                if (results.TryGetValue(kvp.Key, out TResult? result))
                {
                    kvp.Value.Tcs.TrySetResult(result);
                }
                else
                {
                    kvp.Value.Tcs.TrySetException(
                        new KeyNotFoundException(
                            $"Batch handler did not return a result for key '{kvp.Key}' " +
                            $"(query type: {typeof(TQuery).Name})."));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error draining batch for {QueryType}.", typeof(TQuery).Name);

            foreach (BatchEntry entry in toProcess.Values)
            {
                entry.Tcs.TrySetException(ex);
            }
        }
        finally
        {
            _flushLock.Release();
        }
    }

    private readonly record struct EnqueueArgs(TQuery Query);

    internal sealed record BatchEntry(TQuery Query, TaskCompletionSource<TResult> Tcs);
}
