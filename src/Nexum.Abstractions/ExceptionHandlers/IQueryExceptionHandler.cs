namespace Nexum.Abstractions;

/// <summary>
/// Handles exceptions thrown during query dispatch.
/// Exception handlers are side-effect only and must NOT swallow exceptions.
/// </summary>
/// <typeparam name="TQuery">The query type. Contravariant. Constrained to <see cref="IQuery"/> (non-generic marker).</typeparam>
/// <typeparam name="TException">The exception type. Contravariant.</typeparam>
public interface IQueryExceptionHandler<in TQuery, in TException>
    where TQuery : IQuery
    where TException : Exception
{
    /// <summary>
    /// Handles the exception as a side-effect. The original exception is always re-thrown by the dispatcher.
    /// </summary>
    /// <param name="query">The query that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Optional cancellation token.</param>
    ValueTask HandleAsync(TQuery query, TException exception, CancellationToken ct = default);
}
