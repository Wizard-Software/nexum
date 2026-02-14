namespace Nexum.Abstractions;

/// <summary>
/// Specifies the lifetime of a handler or behavior registered with Nexum.
/// Uses a dedicated enum instead of <c>ServiceLifetime</c> to preserve
/// the zero-dependency constraint of <c>Nexum.Abstractions</c>.
/// </summary>
public enum NexumLifetime
{
    /// <summary>
    /// A new instance is created every time it is requested.
    /// This is the default lifetime for behaviors and exception handlers.
    /// </summary>
    Transient,

    /// <summary>
    /// A single instance is created per scope (typically per request).
    /// This is the default lifetime for handlers.
    /// </summary>
    Scoped,

    /// <summary>
    /// A single instance is shared across the entire application lifetime.
    /// </summary>
    Singleton
}
