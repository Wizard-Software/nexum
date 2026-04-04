using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nexum.Abstractions;
using Nexum.Batching.Internal;

namespace Nexum.Batching;

/// <summary>
/// Decorator for <see cref="IQueryDispatcher"/> that batches queries
/// when an <see cref="IBatchQueryHandler{TQuery,TKey,TResult}"/> is registered.
/// Queries without a batch handler are passed through to the inner dispatcher.
/// <see cref="IQueryDispatcher.StreamAsync{TResult}"/> always passes through.
/// </summary>
internal sealed class BatchingQueryDispatcher(
    IQueryDispatcher inner,
    NexumBatchingOptions options,
    IServiceProvider serviceProvider) : IQueryDispatcher, IAsyncDisposable
{
    // Key: query runtime type → Value: BatchBufferAccessor<TResult> (boxed) or null (no batch handler)
    private readonly ConcurrentDictionary<Type, object?> _buffers = new();

    public ValueTask<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        Type queryType = query.GetType();

        object? bufferObj = _buffers.GetOrAdd(queryType, static (type, state) =>
        {
            (NexumBatchingOptions opts, IServiceProvider sp, Type resultType) = state;
            return CreateBufferAccessor(type, resultType, opts, sp);
        }, (options, serviceProvider, typeof(TResult)));

        if (bufferObj is null)
        {
            return inner.DispatchAsync(query, ct);
        }

        var accessor = (BatchBufferAccessor<TResult>)bufferObj;
        return accessor.EnqueueAsync(query, ct);
    }

    public IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IStreamQuery<TResult> query,
        CancellationToken ct = default)
    {
        return inner.StreamAsync(query, ct);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (object? buffer in _buffers.Values)
        {
            if (buffer is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Runtime batch resolution; types preserved by AddNexumBatching() DI registrations.")]
    private static object? CreateBufferAccessor(
        Type queryType, Type resultType,
        NexumBatchingOptions opts, IServiceProvider sp)
    {
        IEnumerable<BatchHandlerRegistration> registrations =
            sp.GetRequiredService<IEnumerable<BatchHandlerRegistration>>();

        BatchHandlerRegistration? registration = registrations
            .FirstOrDefault(r => r.QueryType == queryType && r.ResultType == resultType);

        if (registration is null)
        {
            return null;
        }

        Type bufferType = typeof(BatchBuffer<,,>)
            .MakeGenericType(queryType, registration.KeyType, resultType);

        Type accessorType = typeof(TypedBatchBufferAccessor<,,>)
            .MakeGenericType(queryType, registration.KeyType, resultType);

        ILogger logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger(bufferType);
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        object buffer = Activator.CreateInstance(bufferType, opts, scopeFactory, logger)!;
        return Activator.CreateInstance(accessorType, buffer)!;
    }
}
