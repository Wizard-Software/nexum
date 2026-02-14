using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nexum.Abstractions;
using Nexum.Results.FluentValidation;
using Nexum.Results.FluentValidation.Internal;

namespace Nexum.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering FluentValidation integration with Nexum.
/// </summary>
public static class NexumFluentValidationServiceCollectionExtensions
{
    /// <summary>
    /// Registers FluentValidation integration with Nexum: adds the validation
    /// command behavior, default result failure factory, and optionally discovers
    /// validators from assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="behaviorOrder">Optional override for behavior pipeline order (default: 0 from attribute).</param>
    /// <param name="behaviorLifetime">Lifetime for the validation behavior (default: Transient).</param>
    /// <param name="validatorLifetime">Lifetime for discovered validators (default: Transient).</param>
    /// <param name="assemblies">Assemblies to scan for IValidator implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresUnreferencedCode("Default IResultFailureFactory uses reflection. " +
        "For NativeAOT, register a custom IResultFailureFactory before calling this method.")]
    public static IServiceCollection AddNexumFluentValidation(
        this IServiceCollection services,
        int? behaviorOrder = null,
        NexumLifetime behaviorLifetime = NexumLifetime.Transient,
        ServiceLifetime validatorLifetime = ServiceLifetime.Transient,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddNexumBehavior(
            typeof(FluentValidationCommandBehavior<,>),
            order: behaviorOrder,
            lifetime: behaviorLifetime);

        services.TryAddSingleton<IResultFailureFactory, DefaultResultFailureFactory>();

        foreach (var assembly in assemblies)
        {
            services.AddValidatorsFromAssembly(assembly,
                lifetime: validatorLifetime);
        }

        return services;
    }
}
