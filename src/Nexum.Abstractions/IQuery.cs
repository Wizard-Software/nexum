namespace Nexum.Abstractions;

/// <summary>
/// Non-generic marker interface for all queries.
/// Enables exception handler constraints without requiring the result type parameter.
/// </summary>
public interface IQuery;

/// <summary>
/// Represents a query that returns a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of result produced by the query. Covariant.</typeparam>
public interface IQuery<out TResult> : IQuery;
