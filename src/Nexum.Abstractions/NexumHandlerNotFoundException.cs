namespace Nexum.Abstractions;

/// <summary>
/// Exception thrown when no handler is registered for a dispatched command or query.
/// </summary>
/// <remarks>
/// Inherits from <see cref="InvalidOperationException"/> for backward compatibility —
/// existing <c>catch (InvalidOperationException)</c> blocks will still catch this exception.
/// </remarks>
public sealed class NexumHandlerNotFoundException : InvalidOperationException
{
    /// <summary>Gets the type of the request (command/query) that had no handler.</summary>
    public Type RequestType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NexumHandlerNotFoundException"/>
    /// with the specified request type and handler interface name.
    /// </summary>
    /// <param name="requestType">The type of the request that had no handler.</param>
    /// <param name="handlerInterfaceName">The name of the handler interface (e.g., "ICommandHandler").</param>
    public NexumHandlerNotFoundException(Type requestType, string handlerInterfaceName)
        : base($"No handler registered for {requestType.Name}. " +
               $"Ensure a handler implementing {handlerInterfaceName}<{requestType.Name}, TResult> " +
               "is registered in the DI container.")
        => RequestType = requestType;
}
