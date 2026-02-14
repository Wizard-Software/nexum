using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register Nexum ASP.NET Core services.
/// </summary>
public static class NexumAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Nexum ASP.NET Core services including ProblemDetails options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional callback to configure <see cref="NexumProblemDetailsOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method does NOT call <c>AddProblemDetails()</c> — the caller owns that registration.
    /// Call <c>builder.Services.AddProblemDetails()</c> before or after this method.
    /// </remarks>
    public static IServiceCollection AddNexumAspNetCore(
        this IServiceCollection services,
        Action<NexumProblemDetailsOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
