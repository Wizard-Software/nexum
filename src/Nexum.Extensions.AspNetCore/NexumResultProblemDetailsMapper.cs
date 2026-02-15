using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Maps Result error objects to <see cref="ProblemDetails"/> responses.
/// Unlike <see cref="NexumProblemDetailsMapper"/> (which maps exceptions),
/// this mapper operates on error objects extracted via <see cref="Nexum.Abstractions.IResultAdapter{TResult}"/>.
/// </summary>
internal static class NexumResultProblemDetailsMapper
{
    /// <summary>
    /// Creates a <see cref="ProblemDetails"/> from the given error object
    /// using the configured <see cref="NexumEndpointOptions.ErrorToProblemDetails"/> callback,
    /// or falls back to a default mapping using <see cref="NexumEndpointOptions.FailureStatusCode"/>.
    /// </summary>
    /// <param name="error">The error object from the Result (boxed via <c>IResultAdapter.GetError</c>).</param>
    /// <param name="options">The endpoint options containing the error mapping configuration.</param>
    /// <returns>A <see cref="ProblemDetails"/> representing the error.</returns>
    public static ProblemDetails CreateProblemDetails(object error, NexumEndpointOptions options)
    {
        // Try custom mapper first
        if (options.ErrorToProblemDetails is not null)
        {
            ProblemDetails? custom = options.ErrorToProblemDetails(error);
            if (custom is not null)
            {
                return custom;
            }
        }

        // Default mapping: any non-null error → ProblemDetails with FailureStatusCode
        return new ProblemDetails
        {
            Status = options.FailureStatusCode,
            Title = "Request Failed",
            Type = $"/errors/status-{options.FailureStatusCode}",
            Detail = error.ToString()
        };
    }
}
