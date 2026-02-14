namespace Nexum.Abstractions;

/// <summary>
/// Enables behaviors to inspect and act on result types without knowing
/// the concrete Result implementation. Uses boxing as a conscious trade-off
/// to support arbitrary Result types (including third-party libraries).
/// </summary>
/// <typeparam name="TResult">The result type to adapt.</typeparam>
public interface IResultAdapter<in TResult>
{
    /// <summary>Determines whether the result represents a success.</summary>
    bool IsSuccess(TResult result);

    /// <summary>Extracts the success value (boxed). Returns null for failure results.</summary>
    object? GetValue(TResult result);

    /// <summary>Extracts the error value (boxed). Returns null for success results.</summary>
    object? GetError(TResult result);
}
