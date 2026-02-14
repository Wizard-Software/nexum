namespace Nexum.Results;

/// <summary>
/// Base error type for the Nexum Result Pattern.
/// </summary>
/// <remarks>
/// Intentionally a non-sealed record class (not record struct) because:
/// <list type="bullet">
///   <item><description>Errors are rare (failure path), so heap allocation is acceptable.</description></item>
///   <item><description>Record class supports inheritance — consumers can derive domain-specific error types.</description></item>
///   <item><description>Nullable reference type (<c>NexumError?</c>) is more ergonomic than <c>default(NexumError)</c> for structs.</description></item>
/// </list>
/// </remarks>
/// <param name="Code">A machine-readable error code (e.g. "VALIDATION_FAILED").</param>
/// <param name="Message">A human-readable error description.</param>
/// <param name="InnerException">An optional exception that caused this error.</param>
public record NexumError(string Code, string Message, Exception? InnerException = null);
