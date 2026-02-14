using Microsoft.AspNetCore.Builder;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to add Nexum middleware.
/// </summary>
public static class NexumApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Nexum middleware to the application pipeline.
    /// The middleware catches Nexum exceptions and returns ProblemDetails responses.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// Requires <see cref="NexumAspNetCoreServiceCollectionExtensions.AddNexumAspNetCore"/> to be called
    /// during service registration. Also requires <c>AddProblemDetails()</c> for ProblemDetails serialization.
    /// </remarks>
    public static IApplicationBuilder UseNexum(this IApplicationBuilder app)
    {
        return app.UseMiddleware<NexumMiddleware>();
    }
}
