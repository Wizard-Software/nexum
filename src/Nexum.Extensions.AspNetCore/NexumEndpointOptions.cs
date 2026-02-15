using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Configuration options for Nexum endpoint behavior including Result-to-HTTP mapping.
/// </summary>
/// <remarks>
/// Packages like <c>Nexum.Results</c> provide extension methods (e.g., <c>UseNexumResultErrorMapping()</c>)
/// that register specific mappings for <c>NexumError → 400</c>, <c>ValidationNexumError → 422</c>.
/// </remarks>
public sealed class NexumEndpointOptions
{
    /// <summary>
    /// Default HTTP status code for successful Result responses.
    /// Default: <see cref="StatusCodes.Status200OK"/> (200).
    /// </summary>
    public int SuccessStatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>
    /// Default HTTP status code for failed Result responses.
    /// Default: <see cref="StatusCodes.Status400BadRequest"/> (400).
    /// </summary>
    public int FailureStatusCode { get; set; } = StatusCodes.Status400BadRequest;

    /// <summary>
    /// Custom error-to-ProblemDetails mapper. When set, this function is invoked with the error object
    /// extracted via <see cref="Nexum.Abstractions.IResultAdapter{TResult}.GetError"/>.
    /// Return <c>null</c> to fall back to default mapping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The error object is always boxed (<c>object</c>) since <see cref="Nexum.Abstractions.IResultAdapter{TResult}"/>
    /// uses boxing as a conscious trade-off to support arbitrary Result types.
    /// </para>
    /// <para>
    /// Example registration from <c>Nexum.Results</c>:
    /// <code>
    /// options.ErrorToProblemDetails = error => error switch
    /// {
    ///     ValidationNexumError ve => new ProblemDetails { Status = 422, ... },
    ///     NexumError ne => new ProblemDetails { Status = 400, ... },
    ///     _ => null
    /// };
    /// </code>
    /// </para>
    /// </remarks>
    public Func<object, ProblemDetails?>? ErrorToProblemDetails { get; set; }
}
