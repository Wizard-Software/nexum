using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.DependencyInjection.Tests;

[Trait("Category", "Integration")]
public sealed class BehaviorOrderingIntegrationTests
{
    // Test 1: Three behaviors registered with explicit order via AddNexumBehavior → execute in correct order
    [Fact]
    public async Task Pipeline_With3BehaviorsWithExplicitOrder_ExecutesInCorrectOrderAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(tracker);

        // Register behaviors with explicit order: C=1, A=2, B=3
        services.AddNexumBehavior(typeof(TrackingBehaviorC<,>), order: 1);
        services.AddNexumBehavior(typeof(TrackingBehaviorA<,>), order: 2);
        services.AddNexumBehavior(typeof(TrackingBehaviorB<,>), order: 3);

        services.AddNexum(assemblies: typeof(BehaviorOrderHandler).Assembly);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act
        await dispatcher.DispatchAsync(new BehaviorOrderCommand("test"), TestContext.Current.CancellationToken);

        // Assert — order C(1), A(2), B(3) — lower first = outermost
        tracker.Should().BeEquivalentTo([
            "BehaviorC:Before",
            "BehaviorA:Before",
            "BehaviorB:Before",
            "Handler",
            "BehaviorB:After",
            "BehaviorA:After",
            "BehaviorC:After"
        ], options => options.WithStrictOrdering());
    }

    // Test 2: Behavior has [BehaviorOrder(1)] attribute, but registered with order: 99 → DI override wins
    [Fact]
    public async Task Pipeline_AddNexumBehaviorOrder_OverridesBehaviorOrderAttributeAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(tracker);

        // OrderedBehaviorWithAttribute has [BehaviorOrder(1)] but we override to 99
        // BehaviorA has no attribute (default 0), registered with order: 1
        services.AddNexumBehavior(typeof(TrackingBehaviorA<,>), order: 1);
        services.AddNexumBehavior(typeof(OrderedBehaviorWithAttribute<,>), order: 99);

        services.AddNexum(assemblies: typeof(BehaviorOrderHandler).Assembly);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act
        await dispatcher.DispatchAsync(new BehaviorOrderCommand("test"), TestContext.Current.CancellationToken);

        // Assert — A(1) before OrderedAttr1(99) — DI override 99 overrules [BehaviorOrder(1)]
        tracker.Should().BeEquivalentTo([
            "BehaviorA:Before",
            "OrderedAttr1:Before",
            "Handler",
            "OrderedAttr1:After",
            "BehaviorA:After"
        ], options => options.WithStrictOrdering());
    }

    // Test 3: Mix of behaviors with explicit order and without → sorted by order, then insertion order
    [Fact]
    public async Task Pipeline_MixedOrderedAndUnordered_CorrectSequenceAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(tracker);

        // A: no order specified (default 0 from attribute/default)
        // B: order 5
        // C: no order specified (default 0)
        // Within order 0: A registered before C → A executes first (stable sort)
        services.AddNexumBehavior(typeof(TrackingBehaviorA<,>));
        services.AddNexumBehavior(typeof(TrackingBehaviorB<,>), order: 5);
        services.AddNexumBehavior(typeof(TrackingBehaviorC<,>));

        services.AddNexum(assemblies: typeof(BehaviorOrderHandler).Assembly);

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act
        await dispatcher.DispatchAsync(new BehaviorOrderCommand("test"), TestContext.Current.CancellationToken);

        // Assert — A(0), C(0), B(5) — same order preserves insertion order
        tracker.Should().BeEquivalentTo([
            "BehaviorA:Before",
            "BehaviorC:Before",
            "BehaviorB:Before",
            "Handler",
            "BehaviorB:After",
            "BehaviorC:After",
            "BehaviorA:After"
        ], options => options.WithStrictOrdering());
    }
}
