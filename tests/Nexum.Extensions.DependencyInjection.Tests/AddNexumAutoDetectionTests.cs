using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection.Tests.Fakes;
using Nexum.Extensions.DependencyInjection.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.DependencyInjection.Tests;

[Trait("Category", "Integration")]
public sealed class AddNexumAutoDetectionTests : IDisposable
{
    // Test 1: Assembly scanning mode — handler resolves and dispatches correctly
    [Fact]
    public async Task AddNexum_WithAssemblyScanning_RegistersHandlersAsync()
    {
        // Arrange — use test assembly (no NexumHandlerRegistry) → triggers scanning path
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: typeof(AutoDetectionHandler).Assembly);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act
        var result = await dispatcher.DispatchAsync(new AutoDetectionCommand("scanned"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("scanned");
    }

    // Test 2: Mock NexumHandlerRegistry found in assembly → uses explicit registrations
    [Fact]
    public async Task AddNexum_WithMockNexumHandlerRegistry_UsesExplicitRegistrationsAsync()
    {
        // Arrange — use Fakes assembly which contains NexumHandlerRegistry
        NexumHandlerRegistry.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: typeof(NexumHandlerRegistry).Assembly);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act
        var result = await dispatcher.DispatchAsync(new FakeRegistryCommand("registry"), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("registry");
        NexumHandlerRegistry.WasCalled.Should().BeTrue();
    }

    // Test 3: No registry and no assemblies → throws InvalidOperationException
    [Fact]
    public void AddNexum_NoRegistryNoAssemblies_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddNexum();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No NexumHandlerRegistry found*");
    }

    // Test 4: Configure action applies NexumOptions correctly
    [Fact]
    public async Task AddNexum_WithConfigureAction_AppliesNexumOptionsAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(
            configure: options => options.MaxDispatchDepth = 5,
            assemblies: typeof(AutoDetectionHandler).Assembly);

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<NexumOptions>();

        // Assert
        options.MaxDispatchDepth.Should().Be(5);
    }

    // Test 5: Registry found → scanning skipped (either/or, not priority)
    // Fakes assembly has BOTH NexumHandlerRegistry AND FakeRegistryHandler.
    // When NexumHandlerRegistry is found, scanning is never executed.
    [Fact]
    public async Task AddNexum_RegistryFound_SkipsAssemblyScanningAsync()
    {
        // Arrange
        NexumHandlerRegistry.Reset();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: typeof(NexumHandlerRegistry).Assembly);

        using var sp = services.BuildServiceProvider();

        // Assert — the NexumHandlerRegistry was found and used
        NexumHandlerRegistry.WasCalled.Should().BeTrue();

        // Verify the handler registered by NexumHandlerRegistry works
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var result = await dispatcher.DispatchAsync(new FakeRegistryCommand("from-registry"), TestContext.Current.CancellationToken);
        result.Should().Be("from-registry");
    }

    public void Dispose()
    {
        NexumHandlerRegistry.Reset();
    }
}
