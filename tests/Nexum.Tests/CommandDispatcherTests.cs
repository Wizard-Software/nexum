using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class CommandDispatcherTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task DispatchAsync_WithRegisteredHandler_ReturnsResultAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, TestCommandHandler>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("hello");

        // Act
        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task DispatchAsync_WithoutHandler_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(); // No handler registered
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NexumHandlerNotFoundException>()
            .WithMessage("*No handler registered for*TestCommand*");
    }

    [Fact]
    public async Task DispatchAsync_ExceedingMaxDepth_ThrowsNexumDispatchDepthExceededExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ICommandHandler<ReentrantCommand, string>, ReentrantCommandHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new ReentrantCommand();

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);
        var exception = await act.Should().ThrowAsync<NexumDispatchDepthExceededException>();
        exception.Which.MaxDepth.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WithBehaviors_ExecutesInPipelineOrderAsync()
    {
        // Arrange
        var executionOrder = new List<string>();

        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionOrder);
            services.AddScoped<ICommandHandler<TestCommand, string>, TrackingCommandHandler>();
            services.AddScoped<ICommandBehavior<TestCommand, string>, FirstBehavior>();
            services.AddScoped<ICommandBehavior<TestCommand, string>, SecondBehavior>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        executionOrder.Should().BeEquivalentTo(["First-Before", "Second-Before", "Handler", "Second-After", "First-After"],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_InvokesExceptionHandlerAndRethrowsAsync()
    {
        // Arrange
        var exceptionHandlerInvoked = false;

        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, ThrowingCommandHandler>();
            services.AddScoped<ICommandExceptionHandler<TestCommand, InvalidOperationException>>(_ =>
                new TestCommandExceptionHandler(() => exceptionHandlerInvoked = true));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");

        exceptionHandlerInvoked.Should().BeTrue("exception handler should be invoked before re-throwing");
    }

    [Fact]
    public async Task DispatchAsync_VoidCommand_ReturnsUnitAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestVoidCommand, Unit>, TestVoidCommandHandler>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestVoidCommand();

        // Act
        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task DispatchAsync_CancellationRequested_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel to test propagation

        var capturingHandler = new CapturingCommandHandler();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>>(_ => capturingHandler);
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        await dispatcher.DispatchAsync(command, cts.Token);

        // Assert
        capturingHandler.CapturedToken.Should().Be(cts.Token);
        capturingHandler.CapturedToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task DispatchAsync_NullCommand_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync<string>(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// R7.3: Zero-alloc path — no behaviors, no exception handlers.
    /// Handler is invoked directly without any pipeline wrapping.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NoBehaviorsNoExceptionHandlers_InvokesHandlerDirectlyAsync()
    {
        // Arrange — no behaviors, no exception handlers registered
        var handlerInvoked = false;

        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>>(
                _ => new TrackingInvocationHandler(() => handlerInvoked = true));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act — first dispatch: cold path (populates behavior and exception handler caches)
        var result1 = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Act — second dispatch: hot path (uses zero-alloc direct path)
        handlerInvoked = false;
        var result2 = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result1.Should().Be("test");
        result2.Should().Be("test");
        handlerInvoked.Should().BeTrue("handler must be invoked on zero-alloc hot path");
    }

    /// <summary>
    /// R7.3: Path with no behaviors but exception handler registered.
    /// Exception handler must be invoked when handler throws.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NoBehaviorsWithExceptionHandlers_InvokesExceptionHandlerAsync()
    {
        // Arrange — no behaviors, but an exception handler registered
        var exceptionHandlerInvoked = false;

        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, ThrowingCommandHandler>();
            services.AddScoped<ICommandExceptionHandler<TestCommand, InvalidOperationException>>(_ =>
                new TestCommandExceptionHandler(() => exceptionHandlerInvoked = true));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert — exception handler invoked even on the no-behaviors path
        exceptionHandlerInvoked.Should().BeTrue();
    }

    /// <summary>
    /// R7.3: Full pipeline path — behaviors present, uses existing pipeline.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WithBehaviors_UsesFullPipelineAsync()
    {
        // Arrange — behavior registered
        var behaviorInvoked = false;

        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, TestCommandHandler>();
            services.AddScoped<ICommandBehavior<TestCommand, string>>(
                _ => new TrackingBehavior(() => behaviorInvoked = true));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("test");
        behaviorInvoked.Should().BeTrue("full pipeline must go through the behavior");
    }

    #region Helper Methods

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    #endregion

    #region Test Types

    internal sealed record TestCommand(string Value = "test") : ICommand<string>;

    internal sealed record TestVoidCommand : IVoidCommand;

    internal sealed record ReentrantCommand : ICommand<string>;

    internal sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class TrackingCommandHandler(List<string> executionOrder) : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            executionOrder.Add("Handler");
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class ThrowingCommandHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    internal sealed class CapturingCommandHandler : ICommandHandler<TestCommand, string>
    {
        public CancellationToken CapturedToken { get; private set; }

        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            CapturedToken = ct;
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class TestVoidCommandHandler : ICommandHandler<TestVoidCommand, Unit>
    {
        public ValueTask<Unit> HandleAsync(TestVoidCommand command, CancellationToken ct = default)
        {
            return ValueTask.FromResult(Unit.Value);
        }
    }

    internal sealed class ReentrantCommandHandler(IServiceProvider serviceProvider)
        : ICommandHandler<ReentrantCommand, string>
    {
        public async ValueTask<string> HandleAsync(ReentrantCommand command, CancellationToken ct = default)
        {
            var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
            return await dispatcher.DispatchAsync(command, ct);
        }
    }

    [BehaviorOrder(1)]
    internal sealed class FirstBehavior(List<string> executionOrder) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            executionOrder.Add("First-Before");
            var result = await next(ct);
            executionOrder.Add("First-After");
            return result;
        }
    }

    [BehaviorOrder(2)]
    internal sealed class SecondBehavior(List<string> executionOrder) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            executionOrder.Add("Second-Before");
            var result = await next(ct);
            executionOrder.Add("Second-After");
            return result;
        }
    }

    internal sealed class TestCommandExceptionHandler(Action onInvoked)
        : ICommandExceptionHandler<TestCommand, InvalidOperationException>
    {
        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            onInvoked();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class TrackingInvocationHandler(Action onInvoke) : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            onInvoke();
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class TrackingBehavior(Action onInvoke) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            onInvoke();
            return await next(ct).ConfigureAwait(false);
        }
    }

    #endregion
}
