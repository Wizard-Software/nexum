using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Scans assemblies for Nexum handler implementations via reflection.
/// This is the runtime fallback path for handler discovery when Source Generators are not used.
/// </summary>
/// <remarks>
/// <para>
/// Only discovers handler types:
/// <see cref="ICommandHandler{TCommand, TResult}"/>,
/// <see cref="IQueryHandler{TQuery, TResult}"/>,
/// <see cref="IStreamQueryHandler{TQuery, TResult}"/>,
/// <see cref="INotificationHandler{TNotification}"/>.
/// </para>
/// <para>
/// Behaviors and exception handlers are NOT scanned — they must be registered explicitly
/// via <c>AddNexumBehavior</c> / <c>AddNexumExceptionHandler</c>.
/// </para>
/// <para>
/// This path is NOT compatible with NativeAOT/trimming (ADR-005).
/// The Source Generator path should be preferred for AOT scenarios.
/// </para>
/// </remarks>
internal static class AssemblyScanner
{
    /// <summary>
    /// Handler open generic interfaces to scan for.
    /// Notification handler has 1 generic parameter; command/query/stream handlers have 2.
    /// </summary>
    private static readonly Type[] s_handlerInterfaces =
    [
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
        typeof(IStreamQueryHandler<,>),
        typeof(INotificationHandler<>)
    ];

    /// <summary>
    /// Scans the specified assemblies for handler implementations and returns their registrations.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>A list of handler registrations discovered via reflection.</returns>
    [RequiresUnreferencedCode("Assembly scanning uses reflection to discover handler types.")]
    [RequiresDynamicCode("Assembly scanning uses MakeGenericType for handler interface matching.")]
    public static IReadOnlyList<HandlerRegistration> Scan(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var registrations = new List<HandlerRegistration>();

        foreach (var assembly in assemblies)
        {
            var types = GetExportedTypes(assembly);

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                var lifetime = GetLifetime(type);

                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    var genericDef = iface.GetGenericTypeDefinition();

                    if (Array.IndexOf(s_handlerInterfaces, genericDef) < 0)
                    {
                        continue;
                    }

                    registrations.Add(new HandlerRegistration(iface, type, lifetime));
                }
            }
        }

        return registrations;
    }

    /// <summary>
    /// Gets types from an assembly with graceful handling of <see cref="ReflectionTypeLoadException"/>.
    /// </summary>
    private static IEnumerable<Type> GetExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    /// <summary>
    /// Reads the <see cref="HandlerLifetimeAttribute"/> from the type, defaulting to <see cref="NexumLifetime.Scoped"/>.
    /// </summary>
    private static NexumLifetime GetLifetime(Type type)
    {
        var attribute = type.GetCustomAttribute<HandlerLifetimeAttribute>(inherit: false);
        return attribute?.Lifetime ?? NexumLifetime.Scoped;
    }
}

/// <summary>
/// Represents a handler registration discovered by <see cref="AssemblyScanner"/>.
/// </summary>
/// <param name="ServiceType">The closed handler interface type (e.g., <c>ICommandHandler&lt;CreateOrder, Guid&gt;</c>).</param>
/// <param name="ImplementationType">The concrete handler class type.</param>
/// <param name="Lifetime">The DI lifetime from <see cref="HandlerLifetimeAttribute"/> or default <see cref="NexumLifetime.Scoped"/>.</param>
internal readonly record struct HandlerRegistration(
    Type ServiceType,
    Type ImplementationType,
    NexumLifetime Lifetime);
