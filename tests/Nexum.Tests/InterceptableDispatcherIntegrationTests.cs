using System.Runtime.CompilerServices;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class InterceptableDispatcherIntegrationTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    #region Tier 3 vs Runtime - Commands

    [Fact]
    public async Task Tier3VsRuntime_Command_SameResultAsync()
    {
        // Arrange
        var command = new IntegrationCommand("test-value");

        // Runtime dispatch setup
        ServiceProvider runtimeSp = CreateRuntimeServiceProviderForCommand();
        var runtimeDispatcher = runtimeSp.GetRequiredService<ICommandDispatcher>();

        // Tier 3 (intercepted) dispatch setup
        ServiceProvider tier3Sp = CreateRuntimeServiceProviderForCommand();
        var tier3Dispatcher = tier3Sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)tier3Dispatcher;

        // Act
        string runtimeResult = await runtimeDispatcher.DispatchAsync(command, CancellationToken.None);

        string tier3Result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            static (cmd, sp, ct) => sp.GetRequiredService<IntegrationCommandHandler>().HandleAsync(cmd, ct),
            CancellationToken.None);

        // Assert
        runtimeResult.Should().Be("result:test-value");
        tier3Result.Should().Be("result:test-value");
        tier3Result.Should().Be(runtimeResult);

        // Cleanup
        await runtimeSp.DisposeAsync();
        await tier3Sp.DisposeAsync();
    }

    #endregion

    #region Tier 3 vs Tier 2 - Commands

    [Fact]
    public async Task Tier3VsTier2_Command_SameResultAsync()
    {
        // Arrange
        var command = new IntegrationCommand("tier2-test");

        // Tier 2 dispatch setup (using PipelineRegistryType)
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();

        ServiceProvider tier2Sp = CreateTier2ServiceProviderForCommand();
        var tier2Dispatcher = tier2Sp.GetRequiredService<ICommandDispatcher>();

        // Tier 3 (intercepted) dispatch setup
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();

        ServiceProvider tier3Sp = CreateRuntimeServiceProviderForCommand();
        var tier3Dispatcher = tier3Sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)tier3Dispatcher;

        // Act
        string tier2Result = await tier2Dispatcher.DispatchAsync(command, CancellationToken.None);

        string tier3Result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            static (cmd, sp, ct) => sp.GetRequiredService<IntegrationCommandHandler>().HandleAsync(cmd, ct),
            CancellationToken.None);

        // Assert
        tier2Result.Should().Be("result:tier2-test");
        tier3Result.Should().Be("result:tier2-test");
        tier3Result.Should().Be(tier2Result);

        // Cleanup
        await tier2Sp.DisposeAsync();
        await tier3Sp.DisposeAsync();
    }

    #endregion

    #region Tier 3 vs Runtime - Queries

    [Fact]
    public async Task Tier3VsRuntime_Query_SameResultAsync()
    {
        // Arrange
        var query = new IntegrationQuery("query-value");

        // Runtime dispatch setup
        ServiceProvider runtimeSp = CreateRuntimeServiceProviderForQuery();
        var runtimeDispatcher = runtimeSp.GetRequiredService<IQueryDispatcher>();

        // Tier 3 (intercepted) dispatch setup
        ServiceProvider tier3Sp = CreateRuntimeServiceProviderForQuery();
        var tier3Dispatcher = tier3Sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)tier3Dispatcher;

        // Act
        string runtimeResult = await runtimeDispatcher.DispatchAsync(query, CancellationToken.None);

        string tier3Result = await interceptable.DispatchInterceptedQueryAsync(
            query,
            static (qry, sp, ct) => sp.GetRequiredService<IntegrationQueryHandler>().HandleAsync(qry, ct),
            CancellationToken.None);

        // Assert
        runtimeResult.Should().Be("query-result:query-value");
        tier3Result.Should().Be("query-result:query-value");
        tier3Result.Should().Be(runtimeResult);

        // Cleanup
        await runtimeSp.DisposeAsync();
        await tier3Sp.DisposeAsync();
    }

    #endregion

    #region Tier 3 vs Runtime - Stream Queries

    [Fact]
    public async Task Tier3VsRuntime_StreamQuery_SameResultAsync()
    {
        // Arrange
        var query = new IntegrationStreamQuery(Count: 5);

        // Runtime dispatch setup
        ServiceProvider runtimeSp = CreateRuntimeServiceProviderForStreamQuery();
        var runtimeDispatcher = runtimeSp.GetRequiredService<IQueryDispatcher>();

        // Tier 3 (intercepted) dispatch setup
        ServiceProvider tier3Sp = CreateRuntimeServiceProviderForStreamQuery();
        var tier3Dispatcher = tier3Sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)tier3Dispatcher;

        // Act
        var runtimeResults = new List<int>();
        await foreach (int item in runtimeDispatcher.StreamAsync(query, CancellationToken.None))
        {
            runtimeResults.Add(item);
        }

        var tier3Results = new List<int>();
        await foreach (int item in interceptable.StreamInterceptedAsync(
            query,
            static (qry, sp, ct) => sp.GetRequiredService<IntegrationStreamQueryHandler>().HandleAsync(qry, ct),
            CancellationToken.None))
        {
            tier3Results.Add(item);
        }

        // Assert
        runtimeResults.Should().ContainInOrder(0, 1, 2, 3, 4);
        tier3Results.Should().ContainInOrder(0, 1, 2, 3, 4);
        tier3Results.Should().Equal(runtimeResults);

        // Cleanup
        await runtimeSp.DisposeAsync();
        await tier3Sp.DisposeAsync();
    }

    #endregion

    #region Tier 3 with Behaviors

    [Fact]
    public async Task Tier3_WithBehaviors_ExecutesPipelineCorrectlyAsync()
    {
        // Arrange
        var command = new IntegrationCommand("behavior-test");
        var executionLog = new List<string>();

        ServiceProvider sp = CreateServiceProviderWithBehaviors(executionLog);
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Compiled pipeline with behaviors (simulating Tier 3 generated code)
        async ValueTask<string> CompiledPipelineWithBehaviorsAsync(
            IntegrationCommand cmd,
            IServiceProvider serviceProvider,
            CancellationToken ct)
        {
            var behavior1 = serviceProvider.GetRequiredService<IntegrationCommandBehavior1>();
            var behavior2 = serviceProvider.GetRequiredService<IntegrationCommandBehavior2>();
            var handler = serviceProvider.GetRequiredService<IntegrationCommandHandler>();

            return await behavior1.HandleAsync(cmd,
                async ct2 => await behavior2.HandleAsync(cmd,
                    ct3 => handler.HandleAsync(cmd, ct3),
                    ct2),
                ct);
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipelineWithBehaviorsAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("result:behavior-test");
        executionLog.Should().ContainInOrder(
            "Behavior1:Before",
            "Behavior2:Before",
            "Handler",
            "Behavior2:After",
            "Behavior1:After");

        // Cleanup
        await sp.DisposeAsync();
    }

    #endregion

    #region Tier 3 with Exception Handler

    [Fact]
    public async Task Tier3_WithExceptionHandler_InvokesHandlerAsync()
    {
        // Arrange
        var command = new IntegrationCommand("exception-test");
        bool exceptionHandlerInvoked = false;

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ICommandExceptionHandler<IntegrationCommand, InvalidOperationException>>(_ =>
            new IntegrationCommandExceptionHandler(() => exceptionHandlerInvoked = true));

        using ServiceProvider sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Compiled pipeline that throws
        static ValueTask<string> ThrowingPipelineAsync(
            IntegrationCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            throw new InvalidOperationException("Pipeline exception");
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedCommandAsync(
            command,
            ThrowingPipelineAsync,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Pipeline exception");

        exceptionHandlerInvoked.Should().BeTrue("exception handler should be invoked before re-throwing");
    }

    #endregion

    #region Tier 3 with ValidateScopes — Scoped Resolution

    [Fact]
    public async Task Tier3_WithValidateScopes_Command_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var command = new IntegrationCommand("validate-scopes");

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IntegrationCommandHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Act — must not throw InvalidOperationException for scoped resolution
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            static (cmd, scopedSp, ct) => scopedSp.GetRequiredService<IntegrationCommandHandler>().HandleAsync(cmd, ct),
            CancellationToken.None);

        // Assert
        result.Should().Be("result:validate-scopes");
    }

    [Fact]
    public async Task Tier3_WithValidateScopes_Query_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var query = new IntegrationQuery("validate-scopes-query");

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IntegrationQueryHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Act
        string result = await interceptable.DispatchInterceptedQueryAsync(
            query,
            static (qry, scopedSp, ct) => scopedSp.GetRequiredService<IntegrationQueryHandler>().HandleAsync(qry, ct),
            CancellationToken.None);

        // Assert
        result.Should().Be("query-result:validate-scopes-query");
    }

    [Fact]
    public async Task Tier3_WithValidateScopes_StreamQuery_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var query = new IntegrationStreamQuery(Count: 3);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IntegrationStreamQueryHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Act
        var results = new List<int>();
        await foreach (int item in interceptable.StreamInterceptedAsync(
            query,
            static (qry, scopedSp, ct) => scopedSp.GetRequiredService<IntegrationStreamQueryHandler>().HandleAsync(qry, ct),
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        results.Should().ContainInOrder(0, 1, 2);
        results.Count.Should().Be(3);
    }

    [Fact]
    public async Task Tier3_WithValidateScopes_CommandWithBehaviors_ResolvesScopedServicesAsync()
    {
        // Arrange
        var command = new IntegrationCommand("scoped-behaviors");
        var executionLog = new List<string>();

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IntegrationCommandHandler>(_ => new IntegrationCommandHandler(executionLog));
        services.AddScoped<IntegrationCommandBehavior1>(_ => new IntegrationCommandBehavior1(executionLog));
        services.AddScoped<IntegrationCommandBehavior2>(_ => new IntegrationCommandBehavior2(executionLog));

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        // Compiled pipeline with behaviors — resolves all from scoped provider
        async ValueTask<string> CompiledPipelineWithBehaviorsAsync(
            IntegrationCommand cmd,
            IServiceProvider scopedSp,
            CancellationToken ct)
        {
            var behavior1 = scopedSp.GetRequiredService<IntegrationCommandBehavior1>();
            var behavior2 = scopedSp.GetRequiredService<IntegrationCommandBehavior2>();
            var handler = scopedSp.GetRequiredService<IntegrationCommandHandler>();

            return await behavior1.HandleAsync(cmd,
                async ct2 => await behavior2.HandleAsync(cmd,
                    ct3 => handler.HandleAsync(cmd, ct3),
                    ct2),
                ct);
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipelineWithBehaviorsAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("result:scoped-behaviors");
        executionLog.Should().ContainInOrder(
            "Behavior1:Before",
            "Behavior2:Before",
            "Handler",
            "Behavior2:After",
            "Behavior1:After");
    }

    #endregion

    #region Helper Methods

    private static ServiceProvider CreateRuntimeServiceProviderForCommand()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IntegrationCommandHandler>();
        services.AddScoped<ICommandHandler<IntegrationCommand, string>>(sp => sp.GetRequiredService<IntegrationCommandHandler>());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateTier2ServiceProviderForCommand()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions
        {
            MaxDispatchDepth = int.MaxValue,
            PipelineRegistryType = typeof(TestPipelineRegistry)
        });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IntegrationCommandHandler>();
        services.AddScoped<ICommandHandler<IntegrationCommand, string>>(sp => sp.GetRequiredService<IntegrationCommandHandler>());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateRuntimeServiceProviderForQuery()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IntegrationQueryHandler>();
        services.AddScoped<IQueryHandler<IntegrationQuery, string>>(sp => sp.GetRequiredService<IntegrationQueryHandler>());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateRuntimeServiceProviderForStreamQuery()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IntegrationStreamQueryHandler>();
        services.AddScoped<IStreamQueryHandler<IntegrationStreamQuery, int>>(sp => sp.GetRequiredService<IntegrationStreamQueryHandler>());
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateServiceProviderWithBehaviors(List<string> executionLog)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IntegrationCommandHandler>(_ => new IntegrationCommandHandler(executionLog));
        services.AddScoped<IntegrationCommandBehavior1>(_ => new IntegrationCommandBehavior1(executionLog));
        services.AddScoped<IntegrationCommandBehavior2>(_ => new IntegrationCommandBehavior2(executionLog));
        return services.BuildServiceProvider();
    }

    #endregion

    #region Test Types

    internal sealed record IntegrationCommand(string Value) : ICommand<string>;

    internal sealed record IntegrationQuery(string Value) : IQuery<string>;

    internal sealed record IntegrationStreamQuery(int Count) : IStreamQuery<int>;

    internal sealed class IntegrationCommandHandler : ICommandHandler<IntegrationCommand, string>
    {
        private readonly List<string>? _executionLog;

        public IntegrationCommandHandler(List<string>? executionLog = null)
        {
            _executionLog = executionLog;
        }

        public ValueTask<string> HandleAsync(IntegrationCommand command, CancellationToken ct = default)
        {
            _executionLog?.Add("Handler");
            return ValueTask.FromResult($"result:{command.Value}");
        }
    }

    internal sealed class IntegrationQueryHandler : IQueryHandler<IntegrationQuery, string>
    {
        public ValueTask<string> HandleAsync(IntegrationQuery query, CancellationToken ct = default)
        {
            return ValueTask.FromResult($"query-result:{query.Value}");
        }
    }

    internal sealed class IntegrationStreamQueryHandler : IStreamQueryHandler<IntegrationStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            IntegrationStreamQuery query,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            for (int i = 0; i < query.Count; i++)
            {
                await Task.Yield();
                yield return i;
            }
        }
    }

    internal sealed class IntegrationCommandBehavior1 : ICommandBehavior<IntegrationCommand, string>
    {
        private readonly List<string> _executionLog;

        public IntegrationCommandBehavior1(List<string> executionLog)
        {
            _executionLog = executionLog;
        }

        public async ValueTask<string> HandleAsync(
            IntegrationCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            _executionLog.Add("Behavior1:Before");
            string result = await next(ct);
            _executionLog.Add("Behavior1:After");
            return result;
        }
    }

    internal sealed class IntegrationCommandBehavior2 : ICommandBehavior<IntegrationCommand, string>
    {
        private readonly List<string> _executionLog;

        public IntegrationCommandBehavior2(List<string> executionLog)
        {
            _executionLog = executionLog;
        }

        public async ValueTask<string> HandleAsync(
            IntegrationCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            _executionLog.Add("Behavior2:Before");
            string result = await next(ct);
            _executionLog.Add("Behavior2:After");
            return result;
        }
    }

    internal sealed class IntegrationCommandExceptionHandler : ICommandExceptionHandler<IntegrationCommand, InvalidOperationException>
    {
        private readonly Action _onInvoked;

        public IntegrationCommandExceptionHandler(Action onInvoked)
        {
            _onInvoked = onInvoked;
        }

        public ValueTask HandleAsync(
            IntegrationCommand command,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            _onInvoked();
            return ValueTask.CompletedTask;
        }
    }

    #endregion

    #region Test Pipeline Registry (Tier 2 simulation)

    internal static class TestPipelineRegistry
    {
        private static readonly Dictionary<Type, string> s_commandMethods = new()
        {
            [typeof(IntegrationCommand)] = nameof(Dispatch_IntegrationCommand),
        };

        public static string? GetCommandMethodName(Type commandType)
            => s_commandMethods.TryGetValue(commandType, out var name) ? name : null;

        public static bool IsCompiledBehavior(Type behaviorType)
        {
            _ = behaviorType;
            return false;
        }

        public static Type[] GetCompiledBehaviorTypes()
            => [];

        public static ValueTask<string> Dispatch_IntegrationCommand(
            IntegrationCommand command,
            IServiceProvider sp,
            CancellationToken ct)
        {
            var handler = sp.GetRequiredService<IntegrationCommandHandler>();
            return handler.HandleAsync(command, ct);
        }
    }

    #endregion
}
