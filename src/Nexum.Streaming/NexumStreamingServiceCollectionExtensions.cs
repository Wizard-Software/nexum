using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;

namespace Nexum.Streaming;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register Nexum.Streaming services.
/// </summary>
public static class NexumStreamingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Nexum streaming services, including <see cref="IStreamNotificationPublisher"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">
    /// Optional callback to configure <see cref="NexumStreamingOptions"/> (e.g., merge channel capacity).
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="IStreamNotificationPublisher"/> is registered as a singleton — it is stateless and
    /// thread-safe (consistent with Nexum dispatchers per CONSTITUTION §6).
    /// </para>
    /// <para>
    /// NativeAOT note: <see cref="StreamNotificationPublisher"/> uses reflection-based handler resolution
    /// on the runtime path. Annotated with <see cref="RequiresUnreferencedCodeAttribute"/> and
    /// <see cref="RequiresDynamicCodeAttribute"/>. Use the Source Generator path for trim-safe dispatch.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode(
        "AddNexumStreaming registers StreamNotificationPublisher which uses MakeGenericType at runtime. " +
        "Use the Source Generator path for NativeAOT / trim-safe dispatch.")]
    [RequiresDynamicCode(
        "AddNexumStreaming registers StreamNotificationPublisher which uses MakeGenericType at runtime. " +
        "Use the Source Generator path for NativeAOT-safe dispatch.")]
    public static IServiceCollection AddNexumStreaming(
        this IServiceCollection services,
        Action<NexumStreamingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddOptions<NexumStreamingOptions>();
        services.AddSingleton<IStreamNotificationPublisher, StreamNotificationPublisher>();

        return services;
    }

    /// <summary>
    /// Registers SignalR services required for Nexum stream hubs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The <see cref="ISignalRServerBuilder"/> for further SignalR configuration.</returns>
    /// <remarks>
    /// This is a thin wrapper around <c>AddSignalR()</c> that provides a discoverable entry point
    /// for Nexum's SignalR integration. It can be extended in future releases with Nexum-specific
    /// SignalR configuration (e.g., hub lifetime, error handling policies).
    /// Call <see cref="AddNexumStreaming"/> separately to register the streaming publisher.
    /// </remarks>
    public static ISignalRServerBuilder AddNexumSignalR(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSignalR();
    }
}
