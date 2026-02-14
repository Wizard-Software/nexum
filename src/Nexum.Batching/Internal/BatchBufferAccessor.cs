using Nexum.Abstractions;

namespace Nexum.Batching.Internal;

/// <summary>
/// Type-erased accessor for a batch buffer.
/// Erases TQuery and TKey generic parameters, exposing only TResult
/// which is known at the <see cref="IQueryDispatcher.DispatchAsync{TResult}"/> call site.
/// </summary>
internal abstract class BatchBufferAccessor<TResult> : IAsyncDisposable
{
    public abstract ValueTask<TResult> EnqueueAsync(IQuery<TResult> query, CancellationToken ct);
    public abstract ValueTask DisposeAsync();
}

/// <summary>
/// Typed implementation that wraps <see cref="BatchBuffer{TQuery,TKey,TResult}"/>
/// and performs the safe downcast from <see cref="IQuery{TResult}"/> to TQuery.
/// </summary>
internal sealed class TypedBatchBufferAccessor<TQuery, TKey, TResult>(
    BatchBuffer<TQuery, TKey, TResult> buffer) : BatchBufferAccessor<TResult>
    where TQuery : IQuery<TResult>
    where TKey : notnull
{
    public override ValueTask<TResult> EnqueueAsync(IQuery<TResult> query, CancellationToken ct)
    {
        return buffer.EnqueueAsync((TQuery)query, ct);
    }

    public override ValueTask DisposeAsync() => buffer.DisposeAsync();
}
