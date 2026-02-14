namespace Nexum.Abstractions;

/// <summary>
/// Defines a pipeline behavior that wraps query execution (Russian doll model).
/// </summary>
/// <typeparam name="TQuery">The query type. Contravariant.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface IQueryBehavior<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the query, optionally delegating to the next behavior or handler via <paramref name="next"/>.
    /// </summary>
    ValueTask<TResult> HandleAsync(TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct = default);
}
