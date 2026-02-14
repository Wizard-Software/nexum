namespace Nexum.Abstractions;

/// <summary>
/// Defines a handler for streaming queries of type <typeparamref name="TQuery"/>
/// that produce an asynchronous sequence of <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TQuery">The streaming query type to handle. Contravariant.</typeparam>
/// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
public interface IStreamQueryHandler<in TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    /// <summary>
    /// Handles the specified streaming query, returning an asynchronous sequence of results.
    /// </summary>
    /// <param name="query">The streaming query to handle.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> representing the result stream.</returns>
    IAsyncEnumerable<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
