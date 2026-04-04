using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Internal;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Extension methods for registering Nexum CQRS infrastructure alongside MediatR,
/// enabling gradual handler-by-handler migration.
/// </summary>
public static class MediatRCompatServiceCollectionExtensions
{
    private static readonly Type[] s_nexumHandlerOpenGenericTypes =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IStreamQueryHandler<,>),
        typeof(Nexum.Abstractions.INotificationHandler<>)
    ];

    /// <summary>
    /// Registers Nexum CQRS infrastructure alongside MediatR, enabling dual-dispatch and gradual migration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registration order:
    /// </para>
    /// <list type="number">
    /// <item>Registers MediatR (via <c>AddMediatR</c>) — <c>IMediator</c>, <c>ISender</c>, and all MediatR handlers in the provided assemblies.</item>
    /// <item>Registers Nexum core infrastructure (dispatchers, channel, background service) using <c>TryAdd</c> to avoid overwriting a prior <c>AddNexum()</c> call.</item>
    /// <item>Scans assemblies for dual-interface types and registers MediatR adapters as Nexum handlers (lower priority than native Nexum handlers).</item>
    /// <item>Scans assemblies for native Nexum handlers and registers them.</item>
    /// </list>
    /// <para>
    /// Native Nexum handlers always have priority: adapters use <c>TryAdd</c> and will not overwrite
    /// an already-registered Nexum handler.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureNexum">Optional configuration action for <see cref="NexumOptions"/>.</param>
    /// <param name="configureMediatR">Optional configuration action for MediatR.</param>
    /// <param name="assemblies">Assemblies to scan for handlers and adapters.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover handler and adapter types.")]
    [RequiresDynamicCode("Assembly scanning uses MakeGenericType for handler interface matching.")]
    public static IServiceCollection AddNexumWithMediatRCompat(
        this IServiceCollection services,
        Action<NexumOptions>? configureNexum = null,
        Action<MediatRServiceConfiguration>? configureMediatR = null,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Step 1: Register MediatR (registers IMediator, ISender, IPublisher + MediatR handlers)
        services.AddMediatR(cfg =>
        {
            if (assemblies.Length > 0)
            {
                cfg.RegisterServicesFromAssemblies(assemblies);
            }
            configureMediatR?.Invoke(cfg);
        });

        // Step 2: Register Nexum core infrastructure inline (mirrors AddNexum() but uses TryAdd)
        var options = new NexumOptions();
        configureNexum?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<ExceptionHandlerResolver>();
        services.TryAddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.TryAddSingleton<IQueryDispatcher, QueryDispatcher>();

        // Register bounded channel for FireAndForget notifications (only if not already registered)
        if (!IsServiceRegistered<ChannelWriter<NotificationEnvelope>>(services))
        {
            var channel = Channel.CreateBounded<NotificationEnvelope>(
                new BoundedChannelOptions(options.FireAndForgetChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                });
            services.AddSingleton(channel.Reader);
            services.AddSingleton(channel.Writer);
        }

        services.TryAddSingleton<Nexum.Abstractions.INotificationPublisher>(sp =>
            new NotificationPublisher(
                sp,
                sp.GetRequiredService<NexumOptions>(),
                sp.GetRequiredService<ChannelWriter<NotificationEnvelope>>()));

        if (!IsHostedServiceRegistered<NotificationBackgroundService>(services))
        {
            services.AddSingleton<IHostedService, NotificationBackgroundService>();
        }

        if (assemblies.Length > 0)
        {
            // Step 3: Scan for dual-interface types and register MediatR adapters (TryAdd — lower priority)
            ScanAndRegisterAdapters(services, assemblies);

            // Step 4: Scan for native Nexum handlers (Add — can coexist with adapters for notifications)
            ScanAndRegisterNexumHandlers(services, assemblies);
        }

        return services;
    }

    /// <summary>
    /// Checks whether a service of the specified type is already registered in the collection.
    /// </summary>
    private static bool IsServiceRegistered<TService>(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TService))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether an <see cref="IHostedService"/> with the specified implementation type
    /// is already registered in the collection. Prevents duplicate background service registrations
    /// when both <c>AddNexum()</c> and <c>AddNexumWithMediatRCompat()</c> are called.
    /// </summary>
    private static bool IsHostedServiceRegistered<TImplementation>(IServiceCollection services)
    {
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(TImplementation))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Scans assemblies for types implementing both MediatR and Nexum interfaces,
    /// and registers the appropriate MediatR adapters as Nexum handlers/behaviors.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning uses reflection.")]
    [RequiresDynamicCode("Assembly scanning uses MakeGenericType.")]
    private static void ScanAndRegisterAdapters(IServiceCollection services, Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                TryRegisterDualInterfaceAdapters(services, type);
                TryRegisterBehaviorAdapters(services, type);
            }
        }
    }

    /// <summary>
    /// For a concrete type implementing both a Nexum request interface (ICommand/IQuery)
    /// and a MediatR IRequest interface, registers the corresponding adapter as a Nexum handler.
    /// Uses <c>TryAdd</c> so that native Nexum handlers registered later take priority.
    /// </summary>
    [RequiresDynamicCode("Uses MakeGenericType for adapter registration.")]
    private static void TryRegisterDualInterfaceAdapters(
        IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        foreach (Type iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            Type genericDef = iface.GetGenericTypeDefinition();

            // ICommand<TResult> + IRequest<TResult> → MediatRCommandAdapter<TRequest, TResult>
            if (genericDef == typeof(ICommand<>))
            {
                Type tResult = iface.GetGenericArguments()[0];
                Type mediatRRequestInterface = typeof(global::MediatR.IRequest<>).MakeGenericType(tResult);

                if (mediatRRequestInterface.IsAssignableFrom(type))
                {
                    Type adapterType = typeof(MediatRCommandAdapter<,>).MakeGenericType(type, tResult);
                    Type handlerInterface = typeof(ICommandHandler<,>).MakeGenericType(type, tResult);
                    services.TryAdd(new ServiceDescriptor(handlerInterface, adapterType, ServiceLifetime.Scoped));
                }
            }

            // IQuery<TResult> + IRequest<TResult> → MediatRQueryAdapter<TRequest, TResult>
            else if (genericDef == typeof(IQuery<>))
            {
                Type tResult = iface.GetGenericArguments()[0];
                Type mediatRRequestInterface = typeof(global::MediatR.IRequest<>).MakeGenericType(tResult);

                if (mediatRRequestInterface.IsAssignableFrom(type))
                {
                    Type adapterType = typeof(MediatRQueryAdapter<,>).MakeGenericType(type, tResult);
                    Type handlerInterface = typeof(IQueryHandler<,>).MakeGenericType(type, tResult);
                    services.TryAdd(new ServiceDescriptor(handlerInterface, adapterType, ServiceLifetime.Scoped));
                }
            }
        }

        // MediatR.INotification + Nexum.INotification → MediatRNotificationAdapter<T>
        if (typeof(global::MediatR.INotification).IsAssignableFrom(type)
            && typeof(Nexum.Abstractions.INotification).IsAssignableFrom(type))
        {
            Type adapterType = typeof(MediatRNotificationAdapter<>).MakeGenericType(type);
            Type handlerInterface = typeof(Nexum.Abstractions.INotificationHandler<>).MakeGenericType(type);
            services.TryAdd(new ServiceDescriptor(handlerInterface, adapterType, ServiceLifetime.Scoped));
        }
    }

    /// <summary>
    /// For a concrete type implementing <see cref="global::MediatR.IPipelineBehavior{TRequest,TResponse}"/>
    /// where the request type implements a Nexum ICommand or IQuery interface,
    /// registers the appropriate behavior adapter.
    /// Both command and query adapters are registered when applicable because MediatR pipeline behaviors
    /// do not distinguish between commands and queries.
    /// </summary>
    /// <remarks>
    /// Also registers the concrete behavior implementation under its closed
    /// <c>IPipelineBehavior&lt;TRequest, TResult&gt;</c> interface so the adapter can inject it from DI.
    /// MediatR's <c>RegisterServicesFromAssemblies</c> registers behaviors via open generics only;
    /// the closed registration ensures constructor injection into the adapter works correctly.
    /// </remarks>
    [RequiresDynamicCode("Uses MakeGenericType for adapter registration.")]
    private static void TryRegisterBehaviorAdapters(
        IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        foreach (Type iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            if (iface.GetGenericTypeDefinition() != typeof(global::MediatR.IPipelineBehavior<,>))
            {
                continue;
            }

            Type[] args = iface.GetGenericArguments();
            Type tRequest = args[0];
            Type tResult = args[1];

            bool registeredAsCommand = false;
            bool registeredAsQuery = false;

            // Check if request type implements ICommand<TResult>
            Type commandInterface = typeof(ICommand<>).MakeGenericType(tResult);
            if (commandInterface.IsAssignableFrom(tRequest))
            {
                Type adapterType = typeof(MediatRCommandBehaviorAdapter<,>).MakeGenericType(tRequest, tResult);
                Type behaviorInterface = typeof(ICommandBehavior<,>).MakeGenericType(tRequest, tResult);
                services.TryAdd(new ServiceDescriptor(behaviorInterface, adapterType, ServiceLifetime.Transient));
                registeredAsCommand = true;
            }

            // Check if request type implements IQuery<TResult>
            Type queryInterface = typeof(IQuery<>).MakeGenericType(tResult);
            if (queryInterface.IsAssignableFrom(tRequest))
            {
                Type adapterType = typeof(MediatRQueryBehaviorAdapter<,>).MakeGenericType(tRequest, tResult);
                Type behaviorInterface = typeof(IQueryBehavior<,>).MakeGenericType(tRequest, tResult);
                services.TryAdd(new ServiceDescriptor(behaviorInterface, adapterType, ServiceLifetime.Transient));
                registeredAsQuery = true;
            }

            // Register the concrete behavior as closed IPipelineBehavior<TRequest, TResult> so
            // the adapter can inject it. MediatR registers via open generics; the adapter requires
            // the closed-generic form to be resolvable via DI constructor injection.
            if (registeredAsCommand || registeredAsQuery)
            {
                services.TryAdd(new ServiceDescriptor(iface, type, ServiceLifetime.Transient));
            }
        }
    }

    /// <summary>
    /// Scans assemblies for native Nexum handler types and registers them.
    /// Uses <c>Add</c> (not <c>TryAdd</c>) so multiple
    /// notification handlers can coexist for the same notification type.
    /// </summary>
    [RequiresUnreferencedCode("Assembly scanning uses reflection.")]
    [RequiresDynamicCode("Assembly scanning uses MakeGenericType.")]
    private static void ScanAndRegisterNexumHandlers(IServiceCollection services, Assembly[] assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            foreach (Type type in assembly.GetExportedTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                RegisterNexumHandlerInterfaces(services, type);
            }
        }
    }

    private static void RegisterNexumHandlerInterfaces(
        IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType)
    {
        HandlerLifetimeAttribute? lifetimeAttr = implementationType.GetCustomAttribute<HandlerLifetimeAttribute>();
        ServiceLifetime serviceLifetime = lifetimeAttr is not null
            ? MapNexumLifetime(lifetimeAttr.Lifetime)
            : ServiceLifetime.Scoped;

        foreach (Type iface in implementationType.GetInterfaces())
        {
            if (!iface.IsGenericType)
            {
                continue;
            }

            Type genericDef = iface.GetGenericTypeDefinition();

            foreach (Type handlerType in s_nexumHandlerOpenGenericTypes)
            {
                if (genericDef == handlerType)
                {
                    services.Add(new ServiceDescriptor(iface, implementationType, serviceLifetime));
                }
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="NexumLifetime"/> value to the corresponding <see cref="ServiceLifetime"/>.
    /// </summary>
    private static ServiceLifetime MapNexumLifetime(NexumLifetime lifetime) =>
        lifetime switch
        {
            NexumLifetime.Transient => ServiceLifetime.Transient,
            NexumLifetime.Scoped => ServiceLifetime.Scoped,
            NexumLifetime.Singleton => ServiceLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
        };
}
