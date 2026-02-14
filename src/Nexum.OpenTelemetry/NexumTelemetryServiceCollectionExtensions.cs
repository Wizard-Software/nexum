using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Extension methods for registering Nexum OpenTelemetry instrumentation with the DI container.
/// </summary>
public static class NexumTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing and metrics to the Nexum CQRS dispatchers.
    /// Must be called <b>after</b> <c>AddNexum()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method decorates <see cref="ICommandDispatcher"/>, <see cref="IQueryDispatcher"/>,
    /// and <see cref="INotificationPublisher"/> with tracing and metrics wrappers.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// services.AddNexum(assemblies: typeof(MyHandler).Assembly);
    /// services.AddNexumTelemetry(opts =&gt; opts.EnableMetrics = false);
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add telemetry services to.</param>
    /// <param name="configure">Optional configuration action for <see cref="NexumTelemetryOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddNexum()</c> has not been called before <c>AddNexumTelemetry()</c>.
    /// </exception>
    public static IServiceCollection AddNexumTelemetry(
        this IServiceCollection services,
        Action<NexumTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new NexumTelemetryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<NexumInstrumentation>();

        if (options.EnableTracing || options.EnableMetrics)
        {
            Decorate<ICommandDispatcher>(services, static (inner, sp) =>
                new TracingCommandDispatcher(inner,
                    sp.GetRequiredService<NexumTelemetryOptions>(),
                    sp.GetRequiredService<NexumInstrumentation>()));

            Decorate<IQueryDispatcher>(services, static (inner, sp) =>
                new TracingQueryDispatcher(inner,
                    sp.GetRequiredService<NexumTelemetryOptions>(),
                    sp.GetRequiredService<NexumInstrumentation>()));

            Decorate<INotificationPublisher>(services, static (inner, sp) =>
                new TracingNotificationPublisher(inner,
                    sp.GetRequiredService<NexumTelemetryOptions>(),
                    sp.GetRequiredService<NexumInstrumentation>()));
        }

        return services;
    }

    private static void Decorate<TService>(
        IServiceCollection services,
        Func<TService, IServiceProvider, TService> factory)
        where TService : class
    {
        ServiceDescriptor? original = null;
        int index = -1;

        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(TService))
            {
                original = services[i];
                index = i;
                break;
            }
        }

        if (original is null)
        {
            throw new InvalidOperationException(
                $"No registration found for {typeof(TService).Name}. " +
                $"Ensure AddNexum() is called before AddNexumTelemetry().");
        }

        services[index] = ServiceDescriptor.Describe(
            typeof(TService),
            sp =>
            {
                TService inner = ResolveInner<TService>(original, sp);
                return factory(inner, sp);
            },
            original.Lifetime);
    }

    private static TService ResolveInner<TService>(ServiceDescriptor descriptor, IServiceProvider sp)
        where TService : class
    {
        return descriptor.ImplementationInstance is TService instance
            ? instance
            : descriptor.ImplementationFactory is not null
            ? (TService)descriptor.ImplementationFactory(sp)
            : descriptor.ImplementationType is not null
            ? (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType)
            : throw new InvalidOperationException(
                $"Unable to resolve inner service for {typeof(TService).Name}. " +
                $"The service descriptor has no implementation type, factory, or instance.");
    }
}
