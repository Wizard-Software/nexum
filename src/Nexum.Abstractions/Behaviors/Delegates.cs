namespace Nexum.Abstractions;

/// <summary>
/// Represents the next step in the command pipeline (either the next behavior or the handler).
/// </summary>
/// <typeparam name="TResult">The type of result produced by the command.</typeparam>
/// <param name="ct">The cancellation token to propagate.</param>
public delegate ValueTask<TResult> CommandHandlerDelegate<TResult>(CancellationToken ct);

/// <summary>
/// Represents the next step in the query pipeline (either the next behavior or the handler).
/// </summary>
/// <typeparam name="TResult">The type of result produced by the query.</typeparam>
/// <param name="ct">The cancellation token to propagate.</param>
public delegate ValueTask<TResult> QueryHandlerDelegate<TResult>(CancellationToken ct);

/// <summary>
/// Represents the next step in the streaming query pipeline (either the next behavior or the handler).
/// </summary>
/// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
/// <param name="ct">The cancellation token to propagate.</param>
public delegate IAsyncEnumerable<TResult> StreamQueryHandlerDelegate<TResult>(CancellationToken ct);
