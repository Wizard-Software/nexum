using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.DependencyInjection.Tests.Fakes;

/// <summary>
/// Simulates a Source Generator-produced NexumHandlerRegistry.
/// Must be non-nested, public static, with exact name "NexumHandlerRegistry".
/// Lives in a separate assembly so it doesn't interfere with assembly scanning in other tests.
/// </summary>
public static class NexumHandlerRegistry
{
    public static bool WasCalled { get; private set; }

    public static void AddNexumHandlers(IServiceCollection services)
    {
        WasCalled = true;
        services.AddScoped<ICommandHandler<FakeRegistryCommand, string>, FakeRegistryHandler>();
    }

    public static void Reset() => WasCalled = false;
}
