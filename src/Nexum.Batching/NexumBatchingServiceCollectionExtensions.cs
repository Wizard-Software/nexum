using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Batching.Internal;

namespace Nexum.Batching;

/// <summary>
/// Extension methods for registering Nexum batching / dataloader with the DI container.
/// </summary>
public static class NexumBatchingServiceCollectionExtensions
{
    /// <summary>
    /// Adds batching / dataloader support for Nexum queries.
    /// Must be called <b>after</b> <c>AddNexum()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method scans the provided assemblies for <see cref="IBatchQueryHandler{TQuery,TKey,TResult}"/>
    /// implementations, registers them with the DI container, and decorates <see cref="IQueryDispatcher"/>
    /// with <c>BatchingQueryDispatcher</c> that automatically batches matching queries.
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// services.AddNexum(assemblies: typeof(MyHandler).Assembly);
    /// services.AddNexumBatching(
    ///     opts =&gt; opts.MaxBatchSize = 50,
    ///     typeof(MyBatchHandler).Assembly);
    /// </code>
    /// </para>
    /// <para>
    /// For NativeAOT, register batch handlers manually before calling this method without assemblies.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action for <see cref="NexumBatchingOptions"/>.</param>
    /// <param name="assemblies">Assemblies to scan for <see cref="IBatchQueryHandler{TQuery,TKey,TResult}"/> implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddNexum()</c> has not been called before <c>AddNexumBatching()</c>.
    /// </exception>
    [RequiresUnreferencedCode("Assembly scanning for IBatchQueryHandler uses reflection. " +
        "For NativeAOT, register batch handlers manually before calling this method.")]
    public static IServiceCollection AddNexumBatching(
        this IServiceCollection services,
        Action<NexumBatchingOptions>? configure = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new NexumBatchingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        foreach (Assembly assembly in assemblies)
        {
            ScanAndRegisterBatchHandlers(services, assembly);
        }

        Decorate<IQueryDispatcher>(services, static (inner, sp) =>
            new BatchingQueryDispatcher(
                inner,
                sp.GetRequiredService<NexumBatchingOptions>(),
                sp));

        return services;
    }

    [RequiresUnreferencedCode("Assembly scanning uses reflection.")]
    private static void ScanAndRegisterBatchHandlers(IServiceCollection services, Assembly assembly)
    {
        Type openInterface = typeof(IBatchQueryHandler<,,>);

        foreach (Type type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
            {
                continue;
            }

            foreach (Type iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != openInterface)
                {
                    continue;
                }

                Type[] args = iface.GetGenericArguments();
                Type queryType = args[0];
                Type keyType = args[1];
                Type resultType = args[2];

                // Register the handler as Scoped (default handler lifetime)
                services.AddScoped(iface, type);

                // Register metadata for buffer creation
                services.AddSingleton(new BatchHandlerRegistration(
                    queryType, keyType, resultType, type));
            }
        }
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
                $"Ensure AddNexum() is called before AddNexumBatching().");
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
