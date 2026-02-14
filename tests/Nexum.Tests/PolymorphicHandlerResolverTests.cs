using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Tests;

/// <summary>
/// Unit tests for <see cref="PolymorphicHandlerResolver"/>, which provides cached,
/// thread-safe resolution of handler types by walking the message type hierarchy.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PolymorphicHandlerResolverTests : IDisposable
{
    // Test command types
    private sealed record TestCommand : ICommand<string>;
    private abstract class BaseCommand : ICommand<string> { }
    private sealed class DerivedCommand : BaseCommand { }
    private class MiddleCommand : BaseCommand { }
    private sealed class LeafCommand : MiddleCommand { }

    // Test handler types
    private sealed class TestHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken cancellationToken)
            => ValueTask.FromResult("test");
    }

    private sealed class BaseCommandHandler : ICommandHandler<BaseCommand, string>
    {
        public ValueTask<string> HandleAsync(BaseCommand command, CancellationToken cancellationToken)
            => ValueTask.FromResult("base");
    }

    private sealed class MiddleCommandHandler : ICommandHandler<MiddleCommand, string>
    {
        public ValueTask<string> HandleAsync(MiddleCommand command, CancellationToken cancellationToken)
            => ValueTask.FromResult("middle");
    }

    private sealed class DerivedCommandHandler : ICommandHandler<DerivedCommand, string>
    {
        public ValueTask<string> HandleAsync(DerivedCommand command, CancellationToken cancellationToken)
            => ValueTask.FromResult("derived");
    }

    public void Dispose()
    {
        // Clear the static cache after each test to avoid cross-test contamination
        PolymorphicHandlerResolver.ResetForTesting();
    }

    [Fact]
    public void Resolve_DirectMatch_ReturnsHandlerType()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TestCommand, string>, TestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var result = PolymorphicHandlerResolver.Resolve(
            typeof(TestCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(typeof(ICommandHandler<TestCommand, string>), result);
    }

    [Fact]
    public void Resolve_BaseClassResolution_ReturnsBaseHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        // Register handler only for base class
        services.AddScoped<ICommandHandler<BaseCommand, string>, BaseCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act - resolve with derived command type
        var result = PolymorphicHandlerResolver.Resolve(
            typeof(DerivedCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Assert - should find base class handler via hierarchy walk
        Assert.NotNull(result);
        Assert.Equal(typeof(ICommandHandler<BaseCommand, string>), result);
    }

    [Fact]
    public void Resolve_MultiLevelHierarchy_FindsMostSpecific()
    {
        // Arrange - 3-level hierarchy: LeafCommand : MiddleCommand : BaseCommand
        var services = new ServiceCollection();
        // Register handler at middle level only
        services.AddScoped<ICommandHandler<MiddleCommand, string>, MiddleCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act - resolve with leaf command type
        var result = PolymorphicHandlerResolver.Resolve(
            typeof(LeafCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Assert - should find middle level handler (walks up: LeafCommand → MiddleCommand ✓)
        Assert.NotNull(result);
        Assert.Equal(typeof(ICommandHandler<MiddleCommand, string>), result);
    }

    [Fact]
    public void Resolve_NoHandlerAtConcreteLevel_FallsBackToBase()
    {
        // Arrange
        var services = new ServiceCollection();
        // Only register handler for BaseCommand (not DerivedCommand)
        services.AddScoped<ICommandHandler<BaseCommand, string>, BaseCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act - resolve with DerivedCommand (no direct handler, should fall back to BaseCommand)
        var result = PolymorphicHandlerResolver.Resolve(
            typeof(DerivedCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Assert - should find BaseCommand handler via hierarchy walk
        Assert.NotNull(result);
        Assert.Equal(typeof(ICommandHandler<BaseCommand, string>), result);
    }

    [Fact]
    public void Resolve_SecondCall_ReturnsCachedResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TestCommand, string>, TestHandler>();
        var serviceProvider = services.BuildServiceProvider();

        // Act - first call (cold cache)
        var firstResult = PolymorphicHandlerResolver.Resolve(
            typeof(TestCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Second call (warm cache)
        var secondResult = PolymorphicHandlerResolver.Resolve(
            typeof(TestCommand),
            typeof(ICommandHandler<,>),
            serviceProvider);

        // Assert - both calls return the same result
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult, secondResult);
    }

    [Fact]
    public void ResetForTesting_ClearsCache()
    {
        // Arrange
        var services1 = new ServiceCollection();
        services1.AddScoped<ICommandHandler<DerivedCommand, string>, DerivedCommandHandler>();
        var serviceProvider1 = services1.BuildServiceProvider();

        // First resolution (populates cache) - will find DerivedCommand handler
        var firstResult = PolymorphicHandlerResolver.Resolve(
            typeof(DerivedCommand),
            typeof(ICommandHandler<,>),
            serviceProvider1);

        Assert.NotNull(firstResult);

        // Act - clear the cache
        PolymorphicHandlerResolver.ResetForTesting();

        // Arrange new DI container with different handler (BaseCommand instead)
        var services2 = new ServiceCollection();
        services2.AddScoped<ICommandHandler<BaseCommand, string>, BaseCommandHandler>();
        var serviceProvider2 = services2.BuildServiceProvider();

        // Second resolution (uses new DI, cache should be empty)
        // Should now find BaseCommand handler (not DerivedCommand)
        var secondResult = PolymorphicHandlerResolver.Resolve(
            typeof(DerivedCommand),
            typeof(ICommandHandler<,>),
            serviceProvider2);

        // Assert - cache was cleared, so new DI is used → finds BaseCommand handler via hierarchy walk
        Assert.NotNull(secondResult);
        Assert.Equal(typeof(ICommandHandler<BaseCommand, string>), secondResult);
        Assert.NotEqual(firstResult, secondResult); // Different handler types
    }
}
