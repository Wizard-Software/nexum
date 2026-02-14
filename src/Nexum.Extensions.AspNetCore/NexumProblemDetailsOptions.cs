using Nexum.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Options for configuring how Nexum exceptions are mapped to ProblemDetails responses.
/// </summary>
public sealed class NexumProblemDetailsOptions
{
    /// <summary>
    /// Custom exception-to-ProblemDetails mappings. Factory returns <c>null</c> when exception doesn't match.
    /// Lookup order: exact type first, then base types up the hierarchy.
    /// </summary>
    public Dictionary<Type, Func<Exception, ProblemDetails?>> ExceptionMappings { get; } = new()
    {
        [typeof(NexumHandlerNotFoundException)] = static ex => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Handler Not Found",
            Type = "/errors/handler-not-found",
            Detail = ex.Message
        },
        [typeof(NexumDispatchDepthExceededException)] = static ex => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Dispatch Depth Exceeded",
            Type = "/errors/dispatch-depth-exceeded",
            Detail = ex.Message
        }
    };

    /// <summary>
    /// Whether to include exception details (message, stack trace) in ProblemDetails.Extensions.
    /// Default: <c>false</c>. Enable only in development environments.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
