using Nexum.Abstractions;

namespace Nexum.Batching;

/// <summary>
/// Defines a handler that processes queries in batches for N+1 prevention.
/// </summary>
/// <typeparam name="TQuery">The query type. Must implement <see cref="IQuery{TResult}"/>.</typeparam>
/// <typeparam name="TKey">The key type for deduplication and result mapping.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
/// <remarks>
/// <para>
/// Batch handlers collect individual queries over a configurable time window
/// and execute them as a single batch, reducing database round-trips (N+1 prevention).
/// </para>
/// <para>
/// The handler must return a result for every query in the batch.
/// Missing keys will cause a <see cref="KeyNotFoundException"/> for the affected caller.
/// </para>
/// <para>
/// Example:
/// <code>
/// public sealed class GetOrderByIdBatchHandler
///     : IBatchQueryHandler&lt;GetOrderByIdQuery, Guid, Order&gt;
/// {
///     public Guid GetKey(GetOrderByIdQuery query) =&gt; query.OrderId;
///
///     public async ValueTask&lt;IReadOnlyDictionary&lt;Guid, Order&gt;&gt; HandleAsync(
///         IReadOnlyList&lt;GetOrderByIdQuery&gt; queries, CancellationToken ct)
///     {
///         var ids = queries.Select(q =&gt; q.OrderId).ToList();
///         return await db.Orders
///             .Where(o =&gt; ids.Contains(o.Id))
///             .ToDictionaryAsync(o =&gt; o.Id, ct);
///     }
/// }
/// </code>
/// </para>
/// </remarks>
public interface IBatchQueryHandler<in TQuery, TKey, TResult>
    where TQuery : IQuery<TResult>
    where TKey : notnull
{
    /// <summary>
    /// Extracts the batch key from a query for deduplication and result mapping.
    /// </summary>
    /// <param name="query">The query to extract the key from.</param>
    /// <returns>The key identifying this query within the batch.</returns>
    TKey GetKey(TQuery query);

    /// <summary>
    /// Handles a batch of deduplicated queries.
    /// Must return a result for every query in the batch.
    /// </summary>
    /// <param name="queries">The deduplicated list of queries to process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary mapping query keys to results.</returns>
    ValueTask<IReadOnlyDictionary<TKey, TResult>> HandleAsync(
        IReadOnlyList<TQuery> queries, CancellationToken ct = default);
}
