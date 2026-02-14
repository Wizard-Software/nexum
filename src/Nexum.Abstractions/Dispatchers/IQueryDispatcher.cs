namespace Nexum.Abstractions;

/// <summary>
/// Dispatches queries and streaming queries to their registered handlers.
/// </summary>
public interface IQueryDispatcher
{
    /// <summary>
    /// Dispatches the specified query to its handler asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of result produced by the query.</typeparam>
    /// <param name="query">The query to dispatch.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of the query.</returns>
    ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default);

    /// <summary>
    /// Streams the specified query to its handler, returning an asynchronous sequence.
    /// </summary>
    /// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
    /// <param name="query">The streaming query to execute.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{TResult}"/> representing the stream of results.</returns>
    IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken ct = default);
}
