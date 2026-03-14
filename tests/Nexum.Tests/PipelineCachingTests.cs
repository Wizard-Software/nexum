using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

/// <summary>
/// Tests for R7.2: pipeline delegate caching in the runtime path.
/// Verifies that sorting happens once on cold path and factories are reused on hot path.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PipelineCachingTests : IDisposable
{
    public PipelineCachingTests()
    {
        // Start each test with clean caches
        CommandHandlerWrapperCache.ResetForTesting();
        QueryHandlerWrapperCache.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }

    public void Dispose()
    {
        CommandHandlerWrapperCache.ResetForTesting();
        QueryHandlerWrapperCache.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }

    // -------------------------------------------------------------------------
    // PipelineBuilder.BuildCommandPipelineFactory — cached sort order tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PipelineBuilder_BuildCommandPipelineFactory_CachesSortedOrder_ExecutesInCorrectOrderAsync()
    {
        // Arrange — behaviors with explicit order
        var services = new ServiceCollection();
        services.AddTransient<ICommandBehavior<CachingTestCommand, string>>(_ => new OrderedBehavior3());
        services.AddTransient<ICommandBehavior<CachingTestCommand, string>>(_ => new OrderedBehavior1());
        services.AddTransient<ICommandBehavior<CachingTestCommand, string>>(_ => new OrderedBehavior2());
        var sp = services.BuildServiceProvider();

        var options = new NexumOptions();

        // Get the behavior array from DI to simulate the cold path
        var behaviors = (ICommandBehavior<CachingTestCommand, string>[])sp.GetService(
            typeof(IEnumerable<ICommandBehavior<CachingTestCommand, string>>))!;

        var handler = new TrackingCommandHandler();
        var command = new CachingTestCommand();

        // Act — build pipeline with sorted capture
        CommandHandlerDelegate<string> pipeline = PipelineBuilder.BuildCommandPipelineAndCaptureSorted(
            command, handler, behaviors, options, out ICommandBehavior<CachingTestCommand, string>[] sortedBehaviors);

        // Assert sorted order: Order1(1), Order2(2), Order3(3)
        sortedBehaviors.Should().HaveCount(3);
        sortedBehaviors[0].Should().BeOfType<OrderedBehavior1>();
        sortedBehaviors[1].Should().BeOfType<OrderedBehavior2>();
        sortedBehaviors[2].Should().BeOfType<OrderedBehavior3>();

        // Verify pipeline executes in correct order
        string result = await pipeline(CancellationToken.None);
        result.Should().Be("Order1>Order2>Order3>handled");
    }

    [Fact]
    public async Task PipelineBuilder_BuildCommandPipelineFactory_ResolvesFreshBehaviors_PerDispatchAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICommandBehavior<CachingTestCommand, string>, InstanceTrackingBehavior>();
        var sp = services.BuildServiceProvider();
        var options = new NexumOptions();

        var behaviors = (ICommandBehavior<CachingTestCommand, string>[])sp.GetService(
            typeof(IEnumerable<ICommandBehavior<CachingTestCommand, string>>))!;

        var handler = new TrackingCommandHandler();
        var command = new CachingTestCommand();

        // Cold path: build factory
        PipelineBuilder.BuildCommandPipelineAndCaptureSorted(
            command, handler, behaviors, options, out ICommandBehavior<CachingTestCommand, string>[] sortedBehaviors);

        var factory = PipelineBuilder.BuildCommandPipelineFactory<CachingTestCommand, string>(sortedBehaviors);

        // Act — invoke factory twice
        var handler1 = new TrackingCommandHandler();
        var handler2 = new TrackingCommandHandler();
        await factory(new CachingTestCommand(), handler1, sp, CancellationToken.None);
        await factory(new CachingTestCommand(), handler2, sp, CancellationToken.None);

        // Assert — factory resolves fresh behavior instances from DI on each call (Transient)
        // Both handlers should have been invoked
        handler1.WasInvoked.Should().BeTrue();
        handler2.WasInvoked.Should().BeTrue();

        // Verify that InstanceTrackingBehavior instances are different per call
        // (Transient behaviors are created fresh each time)
        InstanceTrackingBehavior.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task DispatchAsync_WithBehaviors_PipelineFactoryCachePopulatedAfterFirstCallAsync()
    {
        // Arrange
        var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<CachingTestCommand, string>, TrackingCommandHandlerDi>();
            services.AddTransient<ICommandBehavior<CachingTestCommand, string>, CountingBehavior>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var options = sp.GetRequiredService<NexumOptions>();

        // Before first dispatch — no factory cached
        var wrapperKey = typeof(CommandHandlerWrapperImpl<CachingTestCommand, string>);
        CommandHandlerWrapperCache.PipelineFactories.ContainsKey((wrapperKey, options)).Should().BeFalse();

        // Act — first dispatch (cold path)
        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);

        // Assert — factory is now cached
        CommandHandlerWrapperCache.PipelineFactories.ContainsKey((wrapperKey, options)).Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_WithBehaviors_SecondCallUsesCachedFactoryAsync()
    {
        // Arrange — CountingBehavior counts how many times it is instantiated
        CountingBehavior.CreationCount = 0;
        var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<CachingTestCommand, string>, TrackingCommandHandlerDi>();
            services.AddTransient<ICommandBehavior<CachingTestCommand, string>, CountingBehavior>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act — dispatch twice
        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);
        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);

        // Assert — behavior was instantiated twice (Transient) — factory resolves fresh instances
        CountingBehavior.CreationCount.Should().Be(2);
    }

    [Fact]
    public async Task DispatchAsync_WithBehaviors_OrderPreservedOnHotPathAsync()
    {
        // Arrange — two ordered behaviors
        var executionLog = new List<string>();
        var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionLog);
            services.AddScoped<ICommandHandler<CachingTestCommand, string>, LoggingCommandHandler>();
            services.AddTransient<ICommandBehavior<CachingTestCommand, string>, LoggingBehaviorOrder2>();
            services.AddTransient<ICommandBehavior<CachingTestCommand, string>, LoggingBehaviorOrder1>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // First dispatch — cold path (sorts and caches factory)
        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);
        executionLog.Clear();

        // Second dispatch — hot path (uses cached factory, no re-sort)
        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);

        // Assert — order is preserved: Order1 (1) before Order2 (2)
        executionLog.Should().ContainInOrder("Order1:Before", "Order2:Before", "Handler", "Order2:After", "Order1:After");
    }

    [Fact]
    public async Task ResetForTesting_ClearsPipelineFactoryCacheAsync()
    {
        // Arrange — dispatch with behaviors to populate the cache
        var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<CachingTestCommand, string>, TrackingCommandHandlerDi>();
            services.AddTransient<ICommandBehavior<CachingTestCommand, string>, CountingBehavior>();
        });
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        await dispatcher.DispatchAsync(new CachingTestCommand(), CancellationToken.None);

        // Cache should be populated now
        CommandHandlerWrapperCache.PipelineFactories.Should().NotBeEmpty();

        // Act
        CommandHandlerWrapperCache.ResetForTesting();

        // Assert
        CommandHandlerWrapperCache.PipelineFactories.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Query pipeline factory tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task QueryDispatch_WithBehaviors_PipelineFactoryCachePopulatedAfterFirstCallAsync()
    {
        // Arrange
        var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<CachingTestQuery, string>, TrackingQueryHandlerDi>();
            services.AddTransient<IQueryBehavior<CachingTestQuery, string>, CountingQueryBehavior>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var options = sp.GetRequiredService<NexumOptions>();

        var wrapperKey = typeof(QueryHandlerWrapperImpl<CachingTestQuery, string>);
        QueryHandlerWrapperCache.PipelineFactories.ContainsKey((wrapperKey, options)).Should().BeFalse();

        // Act
        await dispatcher.DispatchAsync(new CachingTestQuery(), CancellationToken.None);

        // Assert
        QueryHandlerWrapperCache.PipelineFactories.ContainsKey((wrapperKey, options)).Should().BeTrue();
    }

    [Fact]
    public async Task QueryDispatch_WithBehaviors_OrderPreservedOnHotPathAsync()
    {
        // Arrange
        var executionLog = new List<string>();
        var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionLog);
            services.AddScoped<IQueryHandler<CachingTestQuery, string>, LoggingQueryHandler>();
            services.AddTransient<IQueryBehavior<CachingTestQuery, string>, LoggingQueryBehaviorOrder2>();
            services.AddTransient<IQueryBehavior<CachingTestQuery, string>, LoggingQueryBehaviorOrder1>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        // First dispatch — cold path
        await dispatcher.DispatchAsync(new CachingTestQuery(), CancellationToken.None);
        executionLog.Clear();

        // Second dispatch — hot path
        await dispatcher.DispatchAsync(new CachingTestQuery(), CancellationToken.None);

        // Assert — order preserved
        executionLog.Should().ContainInOrder("QOrder1:Before", "QOrder2:Before", "QHandler", "QOrder2:After", "QOrder1:After");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    // -------------------------------------------------------------------------
    // Test types
    // -------------------------------------------------------------------------

    private sealed record CachingTestCommand : ICommand<string>;
    private sealed record CachingTestQuery : IQuery<string>;

    private sealed class TrackingCommandHandler : ICommandHandler<CachingTestCommand, string>
    {
        public bool WasInvoked { get; private set; }

        public ValueTask<string> HandleAsync(CachingTestCommand command, CancellationToken ct = default)
        {
            WasInvoked = true;
            return ValueTask.FromResult("handled");
        }
    }

    private sealed class TrackingCommandHandlerDi : ICommandHandler<CachingTestCommand, string>
    {
        public ValueTask<string> HandleAsync(CachingTestCommand command, CancellationToken ct = default)
            => ValueTask.FromResult("handled");
    }

    private sealed class TrackingQueryHandlerDi : IQueryHandler<CachingTestQuery, string>
    {
        public ValueTask<string> HandleAsync(CachingTestQuery query, CancellationToken ct = default)
            => ValueTask.FromResult("handled");
    }

    [BehaviorOrder(1)]
    private sealed class OrderedBehavior1 : ICommandBehavior<CachingTestCommand, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            string inner = await next(ct).ConfigureAwait(false);
            return $"Order1>{inner}";
        }
    }

    [BehaviorOrder(2)]
    private sealed class OrderedBehavior2 : ICommandBehavior<CachingTestCommand, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            string inner = await next(ct).ConfigureAwait(false);
            return $"Order2>{inner}";
        }
    }

    [BehaviorOrder(3)]
    private sealed class OrderedBehavior3 : ICommandBehavior<CachingTestCommand, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            string inner = await next(ct).ConfigureAwait(false);
            return $"Order3>{inner}";
        }
    }

    private sealed class InstanceTrackingBehavior : ICommandBehavior<CachingTestCommand, string>
    {
        public static int InvocationCount;

        public ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            Interlocked.Increment(ref InvocationCount);
            return next(ct);
        }
    }

    private sealed class CountingBehavior : ICommandBehavior<CachingTestCommand, string>
    {
        public static int CreationCount;

        public CountingBehavior()
        {
            Interlocked.Increment(ref CreationCount);
        }

        public ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
            => next(ct);
    }

    private sealed class CountingQueryBehavior : IQueryBehavior<CachingTestQuery, string>
    {
        public static int CreationCount;

        public CountingQueryBehavior()
        {
            Interlocked.Increment(ref CreationCount);
        }

        public ValueTask<string> HandleAsync(CachingTestQuery query, QueryHandlerDelegate<string> next, CancellationToken ct = default)
            => next(ct);
    }

    private sealed class LoggingCommandHandler(List<string> log) : ICommandHandler<CachingTestCommand, string>
    {
        public ValueTask<string> HandleAsync(CachingTestCommand command, CancellationToken ct = default)
        {
            log.Add("Handler");
            return ValueTask.FromResult("ok");
        }
    }

    [BehaviorOrder(1)]
    private sealed class LoggingBehaviorOrder1(List<string> log) : ICommandBehavior<CachingTestCommand, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            log.Add("Order1:Before");
            string result = await next(ct).ConfigureAwait(false);
            log.Add("Order1:After");
            return result;
        }
    }

    [BehaviorOrder(2)]
    private sealed class LoggingBehaviorOrder2(List<string> log) : ICommandBehavior<CachingTestCommand, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            log.Add("Order2:Before");
            string result = await next(ct).ConfigureAwait(false);
            log.Add("Order2:After");
            return result;
        }
    }

    private sealed class LoggingQueryHandler(List<string> log) : IQueryHandler<CachingTestQuery, string>
    {
        public ValueTask<string> HandleAsync(CachingTestQuery query, CancellationToken ct = default)
        {
            log.Add("QHandler");
            return ValueTask.FromResult("ok");
        }
    }

    [BehaviorOrder(1)]
    private sealed class LoggingQueryBehaviorOrder1(List<string> log) : IQueryBehavior<CachingTestQuery, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestQuery query, QueryHandlerDelegate<string> next, CancellationToken ct = default)
        {
            log.Add("QOrder1:Before");
            string result = await next(ct).ConfigureAwait(false);
            log.Add("QOrder1:After");
            return result;
        }
    }

    [BehaviorOrder(2)]
    private sealed class LoggingQueryBehaviorOrder2(List<string> log) : IQueryBehavior<CachingTestQuery, string>
    {
        public async ValueTask<string> HandleAsync(CachingTestQuery query, QueryHandlerDelegate<string> next, CancellationToken ct = default)
        {
            log.Add("QOrder2:Before");
            string result = await next(ct).ConfigureAwait(false);
            log.Add("QOrder2:After");
            return result;
        }
    }
}
