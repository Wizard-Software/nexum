namespace Nexum.Abstractions;

/// <summary>
/// Defines a pipeline behavior that wraps streaming query execution (Russian doll model).
/// </summary>
/// <typeparam name="TQuery">The streaming query type. Contravariant.</typeparam>
/// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
public interface IStreamQueryBehavior<in TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    /// <summary>
    /// Handles the streaming query, optionally delegating to the next behavior or handler via <paramref name="next"/>.
    /// </summary>
    IAsyncEnumerable<TResult> HandleAsync(TQuery query, StreamQueryHandlerDelegate<TResult> next, CancellationToken ct = default);
}
