using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Provides cached, thread-safe resolution of handler types by walking the message type hierarchy.
/// </summary>
/// <remarks>
/// <para>
/// This is the PRIMARY resolution mechanism used by all dispatchers. For a given message type
/// (e.g., <c>CreateOrderCommand : BaseOrderCommand : ICommand&lt;Guid&gt;</c>) and a handler
/// open generic (e.g., <c>ICommandHandler&lt;,&gt;</c>), the resolver:
/// </para>
/// <list type="number">
/// <item>Extracts the result type (<c>TResult</c>) from the message's implemented interface (e.g., <c>ICommand&lt;Guid&gt;</c> → <c>Guid</c>)</item>
/// <item>Walks the class hierarchy from concrete to base, constructing closed handler types (e.g., <c>ICommandHandler&lt;CreateOrderCommand, Guid&gt;</c>)</item>
/// <item>Probes the DI container for each candidate handler type</item>
/// <item>Returns the first match found, or <c>null</c> if no handler is registered</item>
/// </list>
/// <para>
/// Results are cached permanently in a thread-safe dictionary using <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> to guarantee single execution
/// of the resolution logic even under concurrent access.
/// </para>
/// <para>
/// After warm-up, resolution is O(1) ~20ns (dictionary lookup only).
/// </para>
/// </remarks>
internal static class PolymorphicHandlerResolver
{
    /// <summary>
    /// Maps handler open generic types to their corresponding message interface open generic types.
    /// </summary>
    /// <remarks>
    /// Used to extract <c>TResult</c> from the message type's implemented interfaces.
    /// For example: <c>ICommandHandler&lt;,&gt;</c> → <c>ICommand&lt;&gt;</c>
    /// </remarks>
    private static readonly Dictionary<Type, Type> s_handlerToMessageInterfaceMap = new()
    {
        [typeof(ICommandHandler<,>)] = typeof(ICommand<>),
        [typeof(IQueryHandler<,>)] = typeof(IQuery<>),
        [typeof(IStreamQueryHandler<,>)] = typeof(IStreamQuery<>)
    };

    /// <summary>
    /// Thread-safe cache mapping (messageType, handlerOpenGeneric) to resolved handler types.
    /// </summary>
    /// <remarks>
    /// The composite key is required because the same message type may be resolved against
    /// different handler open generics (e.g., a type implementing both <c>ICommand</c> and <c>IQuery</c>).
    /// </remarks>
    private static readonly ConcurrentDictionary<(Type MessageType, Type HandlerOpenGeneric), Lazy<Type?>> s_cache = new();

    /// <summary>
    /// Resolves a handler type for the specified message type and handler open generic.
    /// </summary>
    /// <param name="messageType">The concrete message type (e.g., <c>CreateOrderCommand</c>).</param>
    /// <param name="handlerOpenGeneric">The open generic handler interface (e.g., <c>ICommandHandler&lt;,&gt;</c>).</param>
    /// <param name="serviceProvider">The DI container to probe for registered handlers.</param>
    /// <returns>
    /// The closed handler type (e.g., <c>ICommandHandler&lt;CreateOrderCommand, Guid&gt;</c>)
    /// if a matching registration is found in the DI container; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is thread-safe and caches results permanently. The resolution logic
    /// (<see cref="ResolveCore"/>) is executed at most once per unique (messageType, handlerOpenGeneric) pair,
    /// even under concurrent access.
    /// </para>
    /// <para>
    /// After the first resolution (warm-up), subsequent calls return cached results in O(1) time (~20ns).
    /// </para>
    /// </remarks>
    public static Type? Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] Type messageType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerOpenGeneric,
        IServiceProvider serviceProvider)
    {
        var key = (messageType, handlerOpenGeneric);
        var lazy = s_cache.GetOrAdd(key, _ => new Lazy<Type?>(
            () => ResolveCore(messageType, handlerOpenGeneric, serviceProvider),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    /// <summary>
    /// Core resolution logic: extracts <c>TResult</c> and walks the type hierarchy to find a registered handler.
    /// </summary>
    /// <param name="messageType">The concrete message type.</param>
    /// <param name="handlerOpenGeneric">The open generic handler interface.</param>
    /// <param name="serviceProvider">The DI container to probe for registered handlers.</param>
    /// <returns>
    /// The closed handler type if a matching registration is found; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Algorithm:
    /// </para>
    /// <list type="number">
    /// <item>Look up the message interface open generic (e.g., <c>ICommand&lt;&gt;</c>) from the handler open generic</item>
    /// <item>Find the implemented interface on <paramref name="messageType"/> that matches the message interface (e.g., <c>ICommand&lt;Guid&gt;</c>)</item>
    /// <item>Extract <c>TResult</c> from the matched interface's generic arguments</item>
    /// <item>Walk the class hierarchy from concrete to base:
    ///     <list type="bullet">
    ///     <item>Construct the closed handler type: <c>handlerOpenGeneric.MakeGenericType(currentType, TResult)</c></item>
    ///     <item>Check if the DI container has a registration for this handler type</item>
    ///     <item>If found, return the handler type</item>
    ///     <item>Otherwise, move to the base type and repeat</item>
    ///     </list>
    /// </item>
    /// <item>Return <c>null</c> if no match is found in the entire hierarchy</item>
    /// </list>
    /// </remarks>
    private static Type? ResolveCore(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] Type messageType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerOpenGeneric,
        IServiceProvider serviceProvider)
    {
        // Step 1: Get the message interface open generic (e.g., ICommandHandler<,> → ICommand<>)
        if (!s_handlerToMessageInterfaceMap.TryGetValue(handlerOpenGeneric, out var messageInterfaceOpenGeneric))
        {
            return null; // Unknown handler type
        }

        // Step 2: Extract TResult from the message type's implemented interfaces
        var messageInterface = Array.Find(
            messageType.GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == messageInterfaceOpenGeneric);

        if (messageInterface is null)
        {
            return null; // Message type doesn't implement the expected interface
        }

        var resultType = messageInterface.GetGenericArguments()[0]; // Extract TResult

        // Step 3: Walk the class hierarchy from concrete to base (excluding object — violates generic constraints)
        var currentType = messageType;
        while (currentType is not null && currentType != typeof(object))
        {
            // Construct the closed handler type (e.g., ICommandHandler<CreateOrderCommand, Guid>)
            var handlerType = handlerOpenGeneric.MakeGenericType(currentType, resultType);

            // Probe DI for this handler type
            if (serviceProvider.GetService(handlerType) is not null)
            {
                return handlerType; // Found a registered handler
            }

            // Move up the hierarchy
            currentType = currentType.BaseType;
        }

        // No handler found in the entire hierarchy
        return null;
    }

    /// <summary>
    /// Clears the resolution cache. For test isolation only.
    /// </summary>
    /// <remarks>
    /// This method is intended exclusively for unit tests to ensure isolation between test cases.
    /// It should never be called in production code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ResetForTesting()
    {
        s_cache.Clear();
    }
}
