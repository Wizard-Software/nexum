using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Extension methods for <see cref="RouteHandlerBuilder"/> to add Nexum endpoint features.
/// </summary>
public static class NexumRouteHandlerBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="NexumResultEndpointFilter"/> to this endpoint,
    /// which inspects return values via <see cref="Nexum.Abstractions.IResultAdapter{TResult}"/>
    /// and converts failure results to ProblemDetails HTTP responses.
    /// </summary>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for chaining.</returns>
    /// <remarks>
    /// This is an opt-in filter for the runtime path. When using Source Generator-generated
    /// endpoints (<c>MapNexumEndpoints()</c>), Result mapping is handled inline without this filter.
    /// </remarks>
    public static RouteHandlerBuilder WithNexumResultMapping(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<NexumResultEndpointFilter>();
    }
}
