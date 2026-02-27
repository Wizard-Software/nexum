using System.Runtime.CompilerServices;
using AwesomeAssertions.Specialized;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class InterceptableDispatcherTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    #region Command Dispatcher Tests

    [Fact]
    public async Task DispatchInterceptedCommandAsync_WithCompiledPipeline_ReturnsPipelineResultAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForCommands();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("expected-result");

        // Static compiled pipeline that returns the command's Value
        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(cmd.Value);
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipeline,
            CancellationToken.None);

        // Assert
        result.Should().Be("expected-result");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_NullCommand_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForCommands();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(cmd.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedCommandAsync<TestInterceptorCommand, string>(
            null!,
            CompiledPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("command");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_ExceedingMaxDepth_ThrowsDepthExceededExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();

        using ServiceProvider sp = services.BuildServiceProvider();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("test");

        // Enter depth guard manually to be at max depth
        using DispatchDepthGuard.DepthGuardScope guard = DispatchDepthGuard.Enter(1);

        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(cmd.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipeline,
            CancellationToken.None);

        ExceptionAssertions<NexumDispatchDepthExceededException> exception = await act.Should().ThrowAsync<NexumDispatchDepthExceededException>();
        exception.Which.MaxDepth.Should().Be(1);
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_PipelineThrows_InvokesExceptionHandlerAndRethrowsAsync()
    {
        // Arrange
        bool exceptionHandlerInvoked = false;

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ICommandExceptionHandler<TestInterceptorCommand, InvalidOperationException>>(_ =>
            new TestInterceptorCommandExceptionHandler(() => exceptionHandlerInvoked = true));

        using ServiceProvider sp = services.BuildServiceProvider();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("test");

        static ValueTask<string> ThrowingPipeline(
            TestInterceptorCommand _,
            IServiceProvider __,
            CancellationToken ___)
        {
            throw new InvalidOperationException("Pipeline exception");
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedCommandAsync(
            command,
            ThrowingPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Pipeline exception");

        exceptionHandlerInvoked.Should().BeTrue("exception handler should be invoked before re-throwing");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_AsyncPipeline_AwaitsCorrectlyAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForCommands();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("async-result");

        // Async pipeline that yields before returning
        static async ValueTask<string> AsyncPipelineAsync(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            await Task.Yield(); // Force async completion
            return cmd.Value;
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            AsyncPipelineAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("async-result");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_WithMaxDepthDisabled_SkipsDepthGuardAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();

        using ServiceProvider sp = services.BuildServiceProvider();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("no-depth-guard");

        static ValueTask<string> CompiledPipelineAsync(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(cmd.Value);
        }

        // Act (no depth guard entered manually, MaxDepth disabled)
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipelineAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("no-depth-guard");
    }

    #endregion

    #region Query Dispatcher Tests

    [Fact]
    public async Task DispatchInterceptedQueryAsync_WithCompiledPipeline_ReturnsPipelineResultAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("expected-query-result");

        // Static compiled pipeline that returns the query's Value
        static ValueTask<string> CompiledPipeline(
            TestInterceptorQuery qry,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(qry.Value);
        }

        // Act
        string result = await interceptable.DispatchInterceptedQueryAsync(
            query,
            CompiledPipeline,
            CancellationToken.None);

        // Assert
        result.Should().Be("expected-query-result");
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_NullQuery_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        static ValueTask<string> CompiledPipeline(
            TestInterceptorQuery qry,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(qry.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedQueryAsync<TestInterceptorQuery, string>(
            null!,
            CompiledPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_ExceedingMaxDepth_ThrowsDepthExceededExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

        using ServiceProvider sp = services.BuildServiceProvider();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("test");

        // Enter depth guard manually to be at max depth
        using DispatchDepthGuard.DepthGuardScope guard = DispatchDepthGuard.Enter(1);

        static ValueTask<string> CompiledPipeline(
            TestInterceptorQuery qry,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(qry.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedQueryAsync(
            query,
            CompiledPipeline,
            CancellationToken.None);

        ExceptionAssertions<NexumDispatchDepthExceededException> exception = await act.Should().ThrowAsync<NexumDispatchDepthExceededException>();
        exception.Which.MaxDepth.Should().Be(1);
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_PipelineThrows_InvokesExceptionHandlerAndRethrowsAsync()
    {
        // Arrange
        bool exceptionHandlerInvoked = false;

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IQueryExceptionHandler<TestInterceptorQuery, InvalidOperationException>>(_ =>
            new TestInterceptorQueryExceptionHandler(() => exceptionHandlerInvoked = true));

        using ServiceProvider sp = services.BuildServiceProvider();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("test");

        static ValueTask<string> ThrowingPipeline(
            TestInterceptorQuery _,
            IServiceProvider __,
            CancellationToken ___)
        {
            throw new InvalidOperationException("Query pipeline exception");
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedQueryAsync(
            query,
            ThrowingPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Query pipeline exception");

        exceptionHandlerInvoked.Should().BeTrue("exception handler should be invoked before re-throwing");
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_AsyncPipeline_AwaitsCorrectlyAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("async-query-result");

        // Async pipeline that yields before returning
        static async ValueTask<string> AsyncPipelineAsync(
            TestInterceptorQuery qry,
            IServiceProvider _,
            CancellationToken __)
        {
            await Task.Yield(); // Force async completion
            return qry.Value;
        }

        // Act
        string result = await interceptable.DispatchInterceptedQueryAsync(
            query,
            AsyncPipelineAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("async-query-result");
    }

    [Fact]
    public async Task StreamInterceptedAsync_WithCompiledPipeline_ReturnsStreamAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorStreamQuery("stream-test");

        // Static compiled pipeline that returns a stream
        static async IAsyncEnumerable<int> CompiledPipelineAsync(
            TestInterceptorStreamQuery qry,
            IServiceProvider _,
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return 10;
            await Task.Yield();
            yield return 20;
            yield return 30;
        }

        // Act
        var results = new List<int>();
        await foreach (int item in interceptable.StreamInterceptedAsync(
            query,
            CompiledPipelineAsync,
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        results.Should().ContainInOrder(10, 20, 30);
        results.Count.Should().Be(3);
    }

    [Fact]
    public void StreamInterceptedAsync_NullQuery_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        static async IAsyncEnumerable<int> CompiledPipelineAsync(
            TestInterceptorStreamQuery qry,
            IServiceProvider _,
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return 1;
            await Task.CompletedTask;
        }

        // Act - sync lambda because StreamInterceptedAsync throws immediately
        Func<IAsyncEnumerable<int>> act = () => interceptable.StreamInterceptedAsync<TestInterceptorStreamQuery, int>(
            null!,
            CompiledPipelineAsync,
            CancellationToken.None);

        // Assert - sync assertion because throw is immediate
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public void StreamInterceptedAsync_ExceedingMaxDepth_ThrowsDepthExceededExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

        using ServiceProvider sp = services.BuildServiceProvider();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorStreamQuery("test");

        // Enter depth guard manually to be at max depth
        using DispatchDepthGuard.DepthGuardScope guard = DispatchDepthGuard.Enter(1);

        static async IAsyncEnumerable<int> CompiledPipelineAsync(
            TestInterceptorStreamQuery qry,
            IServiceProvider _,
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return 1;
            await Task.CompletedTask;
        }

        // Act - sync lambda because StreamInterceptedAsync throws immediately during setup
        Func<IAsyncEnumerable<int>> act = () => interceptable.StreamInterceptedAsync(
            query,
            CompiledPipelineAsync,
            CancellationToken.None);

        // Assert - sync assertion because throw happens during setup, not iteration
        act.Should().Throw<NexumDispatchDepthExceededException>()
            .WithMessage("Dispatch depth exceeded maximum of 1*");
    }

    #endregion

    #region Scoped Resolution with ValidateScopes Tests

    [Fact]
    public async Task DispatchInterceptedCommandAsync_SyncFastPath_WithValidateScopes_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ScopedCommandHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("scoped-sync");

        // Compiled pipeline that resolves a scoped handler (sync fast path)
        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider scopedProvider,
            CancellationToken ct)
        {
            var handler = scopedProvider.GetRequiredService<ScopedCommandHandler>();
            return handler.HandleAsync(cmd, ct);
        }

        // Act — must not throw "Cannot resolve scoped service from root provider"
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipeline,
            CancellationToken.None);

        // Assert
        result.Should().Be("scoped:scoped-sync");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_AsyncPath_WithValidateScopes_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ScopedCommandHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("scoped-async");

        // Async pipeline that yields (forces async completion path)
        static async ValueTask<string> CompiledPipelineAsync(
            TestInterceptorCommand cmd,
            IServiceProvider scopedProvider,
            CancellationToken ct)
        {
            var handler = scopedProvider.GetRequiredService<ScopedCommandHandler>();
            await Task.Yield();
            return await handler.HandleAsync(cmd, ct);
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipelineAsync,
            CancellationToken.None);

        // Assert
        result.Should().Be("scoped:scoped-async");
    }

    [Fact]
    public async Task DispatchInterceptedCommandAsync_DepthGuardPath_WithValidateScopes_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 16 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ScopedCommandHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("scoped-depth");

        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider scopedProvider,
            CancellationToken ct)
        {
            var handler = scopedProvider.GetRequiredService<ScopedCommandHandler>();
            return handler.HandleAsync(cmd, ct);
        }

        // Act
        string result = await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipeline,
            CancellationToken.None);

        // Assert
        result.Should().Be("scoped:scoped-depth");
    }

    [Fact]
    public async Task DispatchInterceptedQueryAsync_WithValidateScopes_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<ScopedQueryHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("scoped-query");

        static ValueTask<string> CompiledPipeline(
            TestInterceptorQuery qry,
            IServiceProvider scopedProvider,
            CancellationToken ct)
        {
            var handler = scopedProvider.GetRequiredService<ScopedQueryHandler>();
            return handler.HandleAsync(qry, ct);
        }

        // Act
        string result = await interceptable.DispatchInterceptedQueryAsync(
            query,
            CompiledPipeline,
            CancellationToken.None);

        // Assert
        result.Should().Be("scoped:scoped-query");
    }

    [Fact]
    public async Task StreamInterceptedAsync_WithValidateScopes_ResolvesScopedHandlerAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<ScopedStreamQueryHandler>();

        using ServiceProvider sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorStreamQuery("scoped-stream");

        static async IAsyncEnumerable<int> CompiledPipelineAsync(
            TestInterceptorStreamQuery qry,
            IServiceProvider scopedProvider,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var handler = scopedProvider.GetRequiredService<ScopedStreamQueryHandler>();
            await foreach (int item in handler.HandleAsync(qry, ct))
            {
                yield return item;
            }
        }

        // Act
        var results = new List<int>();
        await foreach (int item in interceptable.StreamInterceptedAsync(
            query,
            CompiledPipelineAsync,
            CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        results.Should().ContainInOrder(1, 2, 3);
        results.Count.Should().Be(3);
    }

    #endregion

    #region Cross-Dispatcher NotSupported Tests

    [Fact]
    public async Task CommandDispatcher_DispatchInterceptedQueryAsync_ThrowsNotSupportedExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForCommands();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorQuery("test");

        static ValueTask<string> CompiledPipeline(
            TestInterceptorQuery qry,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(qry.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedQueryAsync(
            query,
            CompiledPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*does not handle queries*");
    }

    [Fact]
    public void CommandDispatcher_StreamInterceptedAsync_ThrowsNotSupportedExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForCommands();
        ICommandDispatcher dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var query = new TestInterceptorStreamQuery("test");

        static async IAsyncEnumerable<int> CompiledPipelineAsync(
            TestInterceptorStreamQuery qry,
            IServiceProvider _,
            [EnumeratorCancellation] CancellationToken ct)
        {
            yield return 1;
            await Task.CompletedTask;
        }

        // Act & Assert - sync because StreamInterceptedAsync throws immediately
        Func<IAsyncEnumerable<int>> act = () => interceptable.StreamInterceptedAsync(
            query,
            CompiledPipelineAsync,
            CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*does not handle stream queries*");
    }

    [Fact]
    public async Task QueryDispatcher_DispatchInterceptedCommandAsync_ThrowsNotSupportedExceptionAsync()
    {
        // Arrange
        using ServiceProvider sp = CreateServiceProviderForQueries();
        IQueryDispatcher dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var interceptable = (IInterceptableDispatcher)dispatcher;

        var command = new TestInterceptorCommand("test");

        static ValueTask<string> CompiledPipeline(
            TestInterceptorCommand cmd,
            IServiceProvider _,
            CancellationToken __)
        {
            return ValueTask.FromResult(cmd.Value);
        }

        // Act & Assert
        Func<Task<string>> act = async () => await interceptable.DispatchInterceptedCommandAsync(
            command,
            CompiledPipeline,
            CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*does not handle commands*");
    }

    #endregion

    #region Helper Methods

    private static ServiceProvider CreateServiceProviderForCommands(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider CreateServiceProviderForQueries(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(
            NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    #endregion

    #region Test Types

    internal sealed record TestInterceptorCommand(string Value) : ICommand<string>;

    internal sealed record TestInterceptorQuery(string Value) : IQuery<string>;

    internal sealed record TestInterceptorStreamQuery(string Value) : IStreamQuery<int>;

    internal sealed class TestInterceptorCommandExceptionHandler(Action onInvoked)
        : ICommandExceptionHandler<TestInterceptorCommand, InvalidOperationException>
    {
        public ValueTask HandleAsync(
            TestInterceptorCommand command,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            onInvoked();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class TestInterceptorQueryExceptionHandler(Action onInvoked)
        : IQueryExceptionHandler<TestInterceptorQuery, InvalidOperationException>
    {
        public ValueTask HandleAsync(
            TestInterceptorQuery query,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            onInvoked();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ScopedCommandHandler : ICommandHandler<TestInterceptorCommand, string>
    {
        public ValueTask<string> HandleAsync(TestInterceptorCommand command, CancellationToken ct = default)
            => ValueTask.FromResult($"scoped:{command.Value}");
    }

    internal sealed class ScopedQueryHandler : IQueryHandler<TestInterceptorQuery, string>
    {
        public ValueTask<string> HandleAsync(TestInterceptorQuery query, CancellationToken ct = default)
            => ValueTask.FromResult($"scoped:{query.Value}");
    }

    internal sealed class ScopedStreamQueryHandler : IStreamQueryHandler<TestInterceptorStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestInterceptorStreamQuery query,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return 1;
            await Task.Yield();
            yield return 2;
            yield return 3;
        }
    }

    #endregion
}
