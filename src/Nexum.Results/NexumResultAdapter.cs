using Nexum.Abstractions;

namespace Nexum.Results;

/// <summary>
/// Adapts <see cref="Result{TValue, TError}"/> for use with Nexum behaviors
/// that inspect result state via <see cref="IResultAdapter{TResult}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
/// <remarks>
/// <see cref="GetValue"/> and <see cref="GetError"/> return boxed values (<c>object?</c>)
/// as a conscious trade-off — the adapter is invoked once per dispatch, not in a hot loop.
/// Returns <see langword="null"/> instead of throwing when accessing the inactive side.
/// </remarks>
public sealed class NexumResultAdapter<TValue, TError> : IResultAdapter<Result<TValue, TError>>
{
    /// <inheritdoc />
    public bool IsSuccess(Result<TValue, TError> result) => result.IsSuccess;

    /// <inheritdoc />
    public object? GetValue(Result<TValue, TError> result) => result.IsSuccess ? result.Value : default;

    /// <inheritdoc />
    public object? GetError(Result<TValue, TError> result) => result.IsFailure ? result.Error : default;
}

/// <summary>
/// Adapts <see cref="Result{TValue}"/> (with <see cref="NexumError"/>) for use with Nexum behaviors
/// that inspect result state via <see cref="IResultAdapter{TResult}"/>.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <remarks>
/// Required because DI resolution is based on the exact <c>TResult</c> type.
/// A handler returning <c>Result&lt;Guid&gt;</c> needs <c>IResultAdapter&lt;Result&lt;Guid&gt;&gt;</c>,
/// not <c>IResultAdapter&lt;Result&lt;Guid, NexumError&gt;&gt;</c>.
/// </remarks>
public sealed class NexumResultAdapter<TValue> : IResultAdapter<Result<TValue>>
{
    /// <inheritdoc />
    public bool IsSuccess(Result<TValue> result) => result.IsSuccess;

    /// <inheritdoc />
    public object? GetValue(Result<TValue> result) => result.IsSuccess ? result.Value : default;

    /// <inheritdoc />
    public object? GetError(Result<TValue> result) => result.IsFailure ? result.Error : default;
}
