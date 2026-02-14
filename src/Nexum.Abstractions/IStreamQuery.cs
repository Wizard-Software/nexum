namespace Nexum.Abstractions;

/// <summary>
/// Represents a streaming query that returns results as an asynchronous sequence of <typeparamref name="TResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// Intentionally does NOT inherit from <see cref="IQuery"/> — streaming queries have
/// distinct dispatch semantics (<c>StreamAsync</c> vs <c>DispatchAsync</c>) and no
/// non-generic marker (no <c>IStreamQueryExceptionHandler</c> by design).
/// </para>
/// </remarks>
/// <typeparam name="TResult">The type of each element in the result stream. Covariant.</typeparam>
public interface IStreamQuery<out TResult>;
