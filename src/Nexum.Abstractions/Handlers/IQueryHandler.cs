namespace Nexum.Abstractions;

/// <summary>
/// Defines a handler for queries of type <typeparamref name="TQuery"/>
/// that produce a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The query type to handle. Contravariant.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the specified query asynchronously.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of handling the query.</returns>
    ValueTask<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
