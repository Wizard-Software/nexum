using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.Pipeline;

[Trait("Category", "E2E")]
public sealed class BehaviorPipelineE2ETests : IDisposable
{
    private IHost? _host;

    public void Dispose() => _host?.Dispose();

    // E2E-010: OuterTrackingBehavior(order=1) wraps InnerTrackingBehavior(order=2).
    // Verifies Russian doll execution: Outer:Before → Inner:Before → handler → Inner:After → Outer:After.
    // Both behaviors have no [BehaviorOrder] attribute, so DI registration order drives the pipeline.
    // OuterTrackingBehavior is registered first → becomes the outermost wrapper.
    [Fact]
    public async Task DispatchAsync_TwoOrderedCommandBehaviors_ExecutesRussianDollOrder()
    {
        // Arrange
        var log = new List<string>();

        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(log);

            // Registration order determines pipeline order when no [BehaviorOrder] attribute is present.
            // OuterTrackingBehavior registered first → outermost (order=1 semantics).
            // InnerTrackingBehavior registered second → innermost behavior (order=2 semantics).
            services.AddNexumBehavior(typeof(OuterTrackingBehavior));
            services.AddNexumBehavior(typeof(InnerTrackingBehavior));
        });

        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await dispatcher.DispatchAsync(new TrackedCommand("ping"), ct);

        // Assert
        result.Should().Be("handled:ping");

        log.Should().HaveCount(4);
        log[0].Should().Be("Outer:Before");
        log[1].Should().Be("Inner:Before");
        log[2].Should().Be("Inner:After");
        log[3].Should().Be("Outer:After");
    }

    // E2E-011: CachingQueryBehavior intercepts GetProductPriceQuery and stores the result.
    // Dispatching the same query twice must invoke the handler exactly once.
    // GetProductPriceHandler is registered as a singleton so InvocationCount is observable.
    [Fact]
    public async Task DispatchAsync_CachingQueryBehavior_HandlerInvokedOnce()
    {
        // Arrange
        var cache = new ConcurrentDictionary<string, object>();
        var handler = new GetProductPriceHandler();

        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(cache);

            // Register handler as singleton to observe InvocationCount across dispatches.
            services.AddSingleton<IQueryHandler<GetProductPriceQuery, decimal>>(handler);

            services.AddNexumBehavior(typeof(CachingQueryBehavior));
        });

        var dispatcher = _host.Services.GetRequiredService<IQueryDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var firstPrice = await dispatcher.DispatchAsync(new GetProductPriceQuery("Widget"), ct);
        var secondPrice = await dispatcher.DispatchAsync(new GetProductPriceQuery("Widget"), ct);

        // Assert
        firstPrice.Should().Be(secondPrice);
        handler.InvocationCount.Should().Be(1, "the caching behavior must short-circuit the second dispatch");
    }

    // E2E-012: FilteringStreamBehavior filters prices from the stream handler.
    // Handler yields [5, 15, 25, 35, 50, 75, 100]. MinPrice=30 must yield [35, 50, 75, 100].
    [Fact]
    public async Task StreamAsync_FilteringStreamBehavior_YieldsOnlyPricesAboveMinimum()
    {
        // Arrange
        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddNexumBehavior(typeof(FilteringStreamBehavior));
        });

        var dispatcher = _host.Services.GetRequiredService<IQueryDispatcher>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var results = new List<decimal>();
        await foreach (var price in dispatcher.StreamAsync(new ListPricesStreamQuery(MinPrice: 30m), ct))
        {
            results.Add(price);
        }

        // Assert
        results.Should().BeEquivalentTo([35m, 50m, 75m, 100m], options => options.WithStrictOrdering());
    }
}
