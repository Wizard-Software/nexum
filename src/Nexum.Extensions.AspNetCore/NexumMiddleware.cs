using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// ASP.NET Core middleware that catches Nexum exceptions and writes ProblemDetails responses.
/// </summary>
/// <remarks>
/// This is a convention-based middleware. It catches exceptions during request processing,
/// maps known Nexum exceptions to ProblemDetails using <see cref="NexumProblemDetailsMapper"/>,
/// and writes them via <see cref="IProblemDetailsService"/>. Unknown exceptions are re-thrown.
/// </remarks>
internal sealed class NexumMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NexumProblemDetailsOptions options = context.RequestServices
                .GetRequiredService<IOptions<NexumProblemDetailsOptions>>().Value;

            if (!NexumProblemDetailsMapper.TryCreateProblemDetails(ex, options, out ProblemDetails? problemDetails))
            {
                throw; // Not a mapped Nexum exception — re-throw
            }

            IProblemDetailsService? problemDetailsService = context.RequestServices
                .GetService<IProblemDetailsService>();

            if (problemDetailsService is null)
            {
                throw; // IProblemDetailsService not registered — re-throw (fail-fast)
            }

            context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problemDetails
            }).ConfigureAwait(false);
        }
    }
}
