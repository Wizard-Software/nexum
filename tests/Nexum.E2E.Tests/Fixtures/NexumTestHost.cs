#pragma warning disable IL2026 // Assembly scanning uses reflection
#pragma warning disable IL3050 // Assembly scanning uses MakeGenericType

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.Fixtures;

public static class NexumTestHost
{
    public static IHost CreateHost(
        Action<IServiceCollection>? configureServices = null,
        Action<NexumOptions>? configureOptions = null)
    {
        return Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices(services =>
            {
                services.AddSingleton(new ConcurrentDictionary<Guid, ItemDto>());

                services.AddNexum(
                    configure: configureOptions,
                    assemblies: typeof(NexumTestHost).Assembly);

                configureServices?.Invoke(services);
            })
            .Build();
    }
}
