using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register Nexum ASP.NET Core services.
/// </summary>
public static class NexumAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers Nexum ASP.NET Core services including ProblemDetails options and endpoint configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureProblemDetails">Optional callback to configure <see cref="NexumProblemDetailsOptions"/>.</param>
    /// <param name="configureEndpoints">Optional callback to configure <see cref="NexumEndpointOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method does NOT call <c>AddProblemDetails()</c> — the caller owns that registration.
    /// Call <c>builder.Services.AddProblemDetails()</c> before or after this method.
    /// </remarks>
    public static IServiceCollection AddNexumAspNetCore(
        this IServiceCollection services,
        Action<NexumProblemDetailsOptions>? configureProblemDetails = null,
        Action<NexumEndpointOptions>? configureEndpoints = null)
    {
        if (configureProblemDetails is not null)
        {
            services.Configure(configureProblemDetails);
        }

        if (configureEndpoints is not null)
        {
            services.Configure(configureEndpoints);
        }

        services.AddTransient<NexumResultEndpointFilter>();

        return services;
    }
}
