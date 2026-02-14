namespace Nexum.Abstractions;

/// <summary>
/// Overrides the default DI lifetime for a handler or behavior.
/// Handlers default to <see cref="NexumLifetime.Scoped"/>;
/// behaviors default to <see cref="NexumLifetime.Transient"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HandlerLifetimeAttribute(NexumLifetime lifetime) : Attribute
{
    /// <summary>Gets the DI lifetime for this handler or behavior.</summary>
    public NexumLifetime Lifetime { get; } = lifetime;
}
