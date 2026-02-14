using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class Tier2PipelineIntegrationTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task DispatchAsync_RuntimeVsTier2_ReturnsIdenticalResultAsync()
    {
        // Arrange — Runtime path
        using var runtimeSp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<Tier2TestCommand, string>, Tier2TestCommandHandler>();
        });
        var runtimeDispatcher = runtimeSp.GetRequiredService<ICommandDispatcher>();
        var command = new Tier2TestCommand("tier2-test");

        var runtimeResult = await runtimeDispatcher.DispatchAsync(command, CancellationToken.None);

        // Reset caches between paths
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();

        // Arrange — Tier 2 path
        using var tier2Sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<Tier2TestCommand, string>, Tier2TestCommandHandler>();
            // Also register handler directly for the compiled pipeline to resolve
            services.AddScoped<Tier2TestCommandHandler>();
        }, options =>
        {
            options.PipelineRegistryType = typeof(TestPipelineRegistry);
        });
        var tier2Dispatcher = tier2Sp.GetRequiredService<ICommandDispatcher>();

        // Act
        var tier2Result = await tier2Dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        tier2Result.Should().Be(runtimeResult);
        tier2Result.Should().Be("tier2-test");
    }

    [Fact]
    public async Task DispatchAsync_Tier2WithBehaviors_ExecutesBehaviorsInCorrectOrderAsync()
    {
        // Arrange
        var executionOrder = new List<string>();

        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionOrder);
            services.AddScoped<ICommandHandler<Tier2BehaviorCommand, string>, Tier2BehaviorCommandHandler>();
            services.AddScoped<Tier2BehaviorCommandHandler>();
            // Register behaviors in DI (compiled pipeline resolves them)
            services.AddScoped<Tier2TrackingBehavior1>();
            services.AddScoped<Tier2TrackingBehavior2>();
            // Also register as ICommandBehavior for runtime-only wrapping (shouldn't duplicate since compiled)
            services.AddScoped<ICommandBehavior<Tier2BehaviorCommand, string>, Tier2TrackingBehavior1>();
            services.AddScoped<ICommandBehavior<Tier2BehaviorCommand, string>, Tier2TrackingBehavior2>();
        }, options =>
        {
            options.PipelineRegistryType = typeof(TestPipelineRegistryWithBehaviors);
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new Tier2BehaviorCommand("test");

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert — behaviors should execute in compiled order (1 → 2 → handler)
        executionOrder.Should().BeEquivalentTo(
            ["Behavior1-Before", "Behavior2-Before", "Handler", "Behavior2-After", "Behavior1-After"],
            options => options.WithStrictOrdering());
        result.Should().Be("test");
    }

    [Fact]
    public async Task DispatchAsync_Tier2FallbackWhenOverrides_UsesRuntimePipelineAsync()
    {
        // Arrange — Set up Tier 2 with a behavior override (ADR-006 guard)
        var executionOrder = new List<string>();

        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionOrder);
            services.AddScoped<ICommandHandler<Tier2FallbackCommand, string>, Tier2FallbackCommandHandler>();
            services.AddScoped<ICommandBehavior<Tier2FallbackCommand, string>, Tier2FallbackBehavior>();
        }, options =>
        {
            options.PipelineRegistryType = typeof(TestPipelineRegistryWithBehaviors);
            // Override a compiled behavior's order → triggers runtime fallback
            options.BehaviorOrderOverrides[typeof(Tier2TrackingBehavior1)] = 99;
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new Tier2FallbackCommand("fallback");

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert — result should be correct (runtime pipeline handles it)
        result.Should().Be("fallback");
        executionOrder.Should().BeEquivalentTo(
            ["FallbackBehavior-Before", "FallbackHandler", "FallbackBehavior-After"],
            options => options.WithStrictOrdering());
    }

    #region Helper Methods

    private static ServiceProvider CreateServiceProvider(
        Action<IServiceCollection>? configure = null,
        Action<NexumOptions>? configureOptions = null)
    {
        var services = new ServiceCollection();
        var options = new NexumOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    #endregion

    #region Test Commands (unique per test to avoid cache contamination)

    internal sealed record Tier2TestCommand(string Value) : ICommand<string>;
    internal sealed record Tier2BehaviorCommand(string Value) : ICommand<string>;
    internal sealed record Tier2FallbackCommand(string Value) : ICommand<string>;

    #endregion

    #region Test Handlers

    internal sealed class Tier2TestCommandHandler : ICommandHandler<Tier2TestCommand, string>
    {
        public ValueTask<string> HandleAsync(Tier2TestCommand command, CancellationToken ct = default)
            => ValueTask.FromResult(command.Value);
    }

    internal sealed class Tier2BehaviorCommandHandler(List<string> executionOrder)
        : ICommandHandler<Tier2BehaviorCommand, string>
    {
        public ValueTask<string> HandleAsync(Tier2BehaviorCommand command, CancellationToken ct = default)
        {
            executionOrder.Add("Handler");
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class Tier2FallbackCommandHandler(List<string> executionOrder)
        : ICommandHandler<Tier2FallbackCommand, string>
    {
        public ValueTask<string> HandleAsync(Tier2FallbackCommand command, CancellationToken ct = default)
        {
            executionOrder.Add("FallbackHandler");
            return ValueTask.FromResult(command.Value);
        }
    }

    #endregion

    #region Test Behaviors

    internal sealed class Tier2TrackingBehavior1(List<string> executionOrder)
        : ICommandBehavior<Tier2BehaviorCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            Tier2BehaviorCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            executionOrder.Add("Behavior1-Before");
            var result = await next(ct).ConfigureAwait(false);
            executionOrder.Add("Behavior1-After");
            return result;
        }
    }

    internal sealed class Tier2TrackingBehavior2(List<string> executionOrder)
        : ICommandBehavior<Tier2BehaviorCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            Tier2BehaviorCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            executionOrder.Add("Behavior2-Before");
            var result = await next(ct).ConfigureAwait(false);
            executionOrder.Add("Behavior2-After");
            return result;
        }
    }

    [BehaviorOrder(1)]
    internal sealed class Tier2FallbackBehavior(List<string> executionOrder)
        : ICommandBehavior<Tier2FallbackCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            Tier2FallbackCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            executionOrder.Add("FallbackBehavior-Before");
            var result = await next(ct).ConfigureAwait(false);
            executionOrder.Add("FallbackBehavior-After");
            return result;
        }
    }

    #endregion

    #region Test Pipeline Registries

    /// <summary>
    /// Simulates a NexumPipelineRegistry with no behaviors (handler-only compiled pipeline).
    /// Matches the contract expected by CommandDispatcher via reflection.
    /// </summary>
    internal static class TestPipelineRegistry
    {
        private static readonly Dictionary<Type, string> s_commandMethods = new()
        {
            [typeof(Tier2TestCommand)] = nameof(Dispatch_Tier2TestCommand),
        };

        public static string? GetCommandMethodName(Type commandType)
            => s_commandMethods.TryGetValue(commandType, out var name) ? name : null;

        public static bool IsCompiledBehavior(Type behaviorType)
        {
            _ = behaviorType; // Parameter required by contract
            return false; // No behaviors in this registry
        }

        public static Type[] GetCompiledBehaviorTypes()
            => []; // No compiled behaviors

        public static ValueTask<string> Dispatch_Tier2TestCommand(
            Tier2TestCommand command,
            IServiceProvider sp,
            CancellationToken ct)
        {
            var handler = sp.GetRequiredService<Tier2TestCommandHandler>();
            return handler.HandleAsync(command, ct);
        }
    }

    /// <summary>
    /// Simulates a NexumPipelineRegistry with compiled behaviors (Russian doll pipeline).
    /// </summary>
    internal static class TestPipelineRegistryWithBehaviors
    {
        private static readonly Dictionary<Type, string> s_commandMethods = new()
        {
            [typeof(Tier2BehaviorCommand)] = nameof(Dispatch_Tier2BehaviorCommand),
        };

        private static readonly HashSet<Type> s_compiledBehaviorTypes =
        [
            typeof(Tier2TrackingBehavior1),
            typeof(Tier2TrackingBehavior2),
        ];

        public static string? GetCommandMethodName(Type commandType)
            => s_commandMethods.TryGetValue(commandType, out var name) ? name : null;

        public static bool IsCompiledBehavior(Type behaviorType)
            => s_compiledBehaviorTypes.Contains(behaviorType);

        public static Type[] GetCompiledBehaviorTypes()
            => [.. s_compiledBehaviorTypes];

        public static ValueTask<string> Dispatch_Tier2BehaviorCommand(
            Tier2BehaviorCommand command,
            IServiceProvider sp,
            CancellationToken ct)
        {
            var handler = sp.GetRequiredService<Tier2BehaviorCommandHandler>();
            var behavior1 = sp.GetRequiredService<Tier2TrackingBehavior1>();
            var behavior2 = sp.GetRequiredService<Tier2TrackingBehavior2>();
            return behavior1.HandleAsync(command,
                ct2 => behavior2.HandleAsync(command,
                ct3 => handler.HandleAsync(command, ct3), ct2),
                ct);
        }
    }

    #endregion
}
