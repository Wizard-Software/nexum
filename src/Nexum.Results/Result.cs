namespace Nexum.Results;

/// <summary>
/// A generic result type representing either a success value or an error.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
/// <remarks>
/// <para>
/// This is a readonly struct using composition — C# structs cannot inherit from other structs.
/// Use <see cref="Ok"/> and <see cref="Fail"/> factory methods to create instances.
/// </para>
/// <para>
/// <c>default(Result&lt;TValue, TError&gt;)</c> has <see cref="IsSuccess"/> = <see langword="false"/>
/// with <see cref="Error"/> = <c>default(TError)</c>. Code should not rely on the default value
/// of any Result version.
/// </para>
/// </remarks>
public readonly struct Result<TValue, TError>
{
    private readonly TValue _value;
    private readonly TError _error;

    /// <summary>Gets a value indicating whether this result represents a success.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether this result represents a failure.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is in a failure state.</exception>
    public TValue Value => IsSuccess
        ? _value
        : throw new InvalidOperationException(
            "Cannot access Value when Result is in failure state. Check IsSuccess before accessing Value.");

    /// <summary>
    /// Gets the error value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is in a success state.</exception>
    public TError Error => IsFailure
        ? _error
        : throw new InvalidOperationException(
            "Cannot access Error when Result is in success state. Check IsFailure before accessing Error.");

    private Result(TValue value)
    {
        _value = value;
        _error = default!;
        IsSuccess = true;
    }

    private Result(TError error, bool _)
    {
        _error = error;
        _value = default!;
        IsSuccess = false;
    }

    /// <summary>Creates a success result with the specified value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A success <see cref="Result{TValue, TError}"/>.</returns>
    public static Result<TValue, TError> Ok(TValue value) => new(value);

    /// <summary>Creates a failure result with the specified error.</summary>
    /// <param name="error">The error value.</param>
    /// <returns>A failure <see cref="Result{TValue, TError}"/>.</returns>
    public static Result<TValue, TError> Fail(TError error) => new(error, false);

    /// <summary>
    /// Transforms the success value using the specified mapping function.
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="mapper">A function to transform the success value.</param>
    /// <returns>A new result with the transformed value, or the original error.</returns>
    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper)
        => IsSuccess ? Result<TNew, TError>.Ok(mapper(Value)) : Result<TNew, TError>.Fail(Error);

    /// <summary>
    /// Chains a result-producing function on the success value (monadic bind).
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="binder">A function that returns a new result.</param>
    /// <returns>The result of the binder function, or the original error.</returns>
    public Result<TNew, TError> Bind<TNew>(Func<TValue, Result<TNew, TError>> binder)
        => IsSuccess ? binder(Value) : Result<TNew, TError>.Fail(Error);

    /// <summary>
    /// Returns the success value if available, or the specified fallback value.
    /// </summary>
    /// <param name="fallback">The value to return when the result is a failure.</param>
    /// <returns>The success value or the fallback.</returns>
    public TValue GetValueOrDefault(TValue fallback) => IsSuccess ? Value : fallback;
}

/// <summary>
/// A result type with <see cref="NexumError"/> as the default error type.
/// Implemented as composition over <see cref="Result{TValue, TError}"/> —
/// C# structs cannot inherit from other structs.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <remarks>
/// <para>
/// Implicit conversion operators enable seamless interop between
/// <see cref="Result{TValue}"/> and <see cref="Result{TValue, TError}"/>
/// where <c>TError</c> is <see cref="NexumError"/>.
/// </para>
/// <para>
/// <c>default(Result&lt;TValue&gt;)</c> is in an invalid state (<see cref="IsFailure"/> = <see langword="true"/>
/// but inner error is <see langword="null"/>). The <see cref="Error"/> property throws
/// <see cref="InvalidOperationException"/> in this case.
/// </para>
/// </remarks>
public readonly struct Result<TValue>
{
    private readonly Result<TValue, NexumError> _inner;

    private Result(Result<TValue, NexumError> inner) => _inner = inner;

    /// <summary>Gets a value indicating whether this result represents a success.</summary>
    public bool IsSuccess => _inner.IsSuccess;

    /// <summary>Gets a value indicating whether this result represents a failure.</summary>
    public bool IsFailure => _inner.IsFailure;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is in a failure state.</exception>
    public TValue Value => _inner.Value;

    /// <summary>
    /// Gets the error value.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the result is in a success state, or when the result is in an invalid
    /// default state (created via <c>default(Result&lt;TValue&gt;)</c>).
    /// </exception>
    public NexumError Error => _inner.IsFailure
        ? _inner.Error ?? throw new InvalidOperationException(
            "Result is in invalid default state. Do not use default(Result<TValue>).")
        : throw new InvalidOperationException(
            "Cannot access Error when Result is in success state.");

    /// <summary>Creates a success result with the specified value.</summary>
    /// <param name="value">The success value.</param>
    /// <returns>A success <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Ok(TValue value) => new(Result<TValue, NexumError>.Ok(value));

    /// <summary>Creates a failure result with the specified error.</summary>
    /// <param name="error">The error.</param>
    /// <returns>A failure <see cref="Result{TValue}"/>.</returns>
    public static Result<TValue> Fail(NexumError error) => new(Result<TValue, NexumError>.Fail(error));

    /// <summary>
    /// Transforms the success value using the specified mapping function.
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="mapper">A function to transform the success value.</param>
    /// <returns>A new result with the transformed value, or the original error.</returns>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper)
        => IsSuccess ? Result<TNew>.Ok(mapper(Value)) : Result<TNew>.Fail(Error);

    /// <summary>
    /// Chains a result-producing function on the success value (monadic bind).
    /// If the result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <typeparam name="TNew">The type of the new success value.</typeparam>
    /// <param name="binder">A function that returns a new result.</param>
    /// <returns>The result of the binder function, or the original error.</returns>
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder)
        => IsSuccess ? binder(Value) : Result<TNew>.Fail(Error);

    /// <summary>
    /// Returns the success value if available, or the specified fallback value.
    /// </summary>
    /// <param name="fallback">The value to return when the result is a failure.</param>
    /// <returns>The success value or the fallback.</returns>
    public TValue GetValueOrDefault(TValue fallback) => IsSuccess ? Value : fallback;

    /// <summary>
    /// Implicitly converts a <see cref="Result{TValue, TError}"/> (with <see cref="NexumError"/>)
    /// to a <see cref="Result{TValue}"/>.
    /// </summary>
    public static implicit operator Result<TValue>(Result<TValue, NexumError> inner) => new(inner);

    /// <summary>
    /// Implicitly converts a <see cref="Result{TValue}"/> to a
    /// <see cref="Result{TValue, TError}"/> (with <see cref="NexumError"/>).
    /// </summary>
    public static implicit operator Result<TValue, NexumError>(Result<TValue> result) => result._inner;
}
