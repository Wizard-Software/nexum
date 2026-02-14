using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class CommandPipelineIntegrationTests : IDisposable
{
    [Fact]
    public async Task FullPipeline_WithBehaviors_ExecutesInCorrectOrderAsync()
    {
        // Arrange
        var executionTracker = new List<string>();
        var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionTracker);
            services.AddScoped<ICommandHandler<TestCommand, string>, TestHandler>();
            services.AddTransient<ICommandBehavior<TestCommand, string>, TrackingBehavior1>();
            services.AddTransient<ICommandBehavior<TestCommand, string>, TrackingBehavior2>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test-value");

        // Act
        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("test-value");
        executionTracker.Should().BeEquivalentTo([
            "Behavior1:Before",
            "Behavior2:Before",
            "Handler",
            "Behavior2:After",
            "Behavior1:After"
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task FullPipeline_WithExceptionHandler_HandlesAndRethrowsAsync()
    {
        // Arrange
        var executionTracker = new List<string>();
        var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionTracker);
            services.AddScoped<ICommandHandler<TestCommand, string>, ThrowingHandler>();
            services.AddTransient<ICommandExceptionHandler<TestCommand, InvalidOperationException>, TrackingExceptionHandler>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test-value");

        // Act
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception from handler");
        executionTracker.Should().Contain("ExceptionHandler");
    }

    [Fact]
    public async Task FullPipeline_NoBehaviors_HandlerExecutedDirectlyAsync()
    {
        // Arrange
        var executionTracker = new List<string>();
        var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(executionTracker);
            services.AddScoped<ICommandHandler<TestCommand, string>, TestHandler>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("direct-value");

        // Act
        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("direct-value");
        executionTracker.Should().BeEquivalentTo(["Handler"], options => options.WithStrictOrdering());
    }

    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
    }

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

    // Test types
    private sealed record TestCommand(string Value) : ICommand<string>;

    private sealed class TestHandler(List<string> tracker) : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            tracker.Add("Handler");
            return new ValueTask<string>(command.Value);
        }
    }

    [BehaviorOrder(1)]
    private sealed class TrackingBehavior1(List<string> tracker) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            tracker.Add("Behavior1:Before");
            var result = await next(ct);
            tracker.Add("Behavior1:After");
            return result;
        }
    }

    [BehaviorOrder(2)]
    private sealed class TrackingBehavior2(List<string> tracker) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            tracker.Add("Behavior2:Before");
            var result = await next(ct);
            tracker.Add("Behavior2:After");
            return result;
        }
    }

    private sealed class ThrowingHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception from handler");
        }
    }

    private sealed class TrackingExceptionHandler(List<string> tracker)
        : ICommandExceptionHandler<TestCommand, InvalidOperationException>
    {
        public ValueTask HandleAsync(
            TestCommand command,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            tracker.Add("ExceptionHandler");
            return default;
        }
    }
}
