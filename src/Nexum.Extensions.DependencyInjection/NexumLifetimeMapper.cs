using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.DependencyInjection;

/// <summary>
/// Maps <see cref="NexumLifetime"/> values to <see cref="ServiceLifetime"/> for DI registration.
/// </summary>
internal static class NexumLifetimeMapper
{
    /// <summary>
    /// Converts a <see cref="NexumLifetime"/> value to the corresponding <see cref="ServiceLifetime"/>.
    /// </summary>
    /// <param name="lifetime">The Nexum lifetime to convert.</param>
    /// <returns>The equivalent <see cref="ServiceLifetime"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="lifetime"/> is not a valid enum value.</exception>
    internal static ServiceLifetime ToServiceLifetime(NexumLifetime lifetime)
    {
        return lifetime switch
        {
            NexumLifetime.Transient => ServiceLifetime.Transient,
            NexumLifetime.Scoped => ServiceLifetime.Scoped,
            NexumLifetime.Singleton => ServiceLifetime.Singleton,
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null)
        };
    }
}
