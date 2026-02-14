using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

/// <summary>
/// Tests that <see cref="CancellationToken"/> is correctly propagated through the behavior pipeline
/// to the handler (CONSTITUTION Z4).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CancellationTokenPropagationTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task DispatchAsync_WithToken_BehaviorReceivesSameTokenAsync()
    {
        // Arrange
        CancellationToken capturedBehaviorToken = default;

        using var cts = new CancellationTokenSource();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, TestHandler>();
            services.AddTransient<ICommandBehavior<TestCommand, string>>(
                _ => new TokenCapturingBehavior(ct => capturedBehaviorToken = ct));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        await dispatcher.DispatchAsync(command, cts.Token);

        // Assert
        capturedBehaviorToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_WithToken_HandlerReceivesSameTokenAsync()
    {
        // Arrange
        CancellationToken capturedHandlerToken = default;

        using var cts = new CancellationTokenSource();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>>(
                _ => new TokenCapturingHandler(ct => capturedHandlerToken = ct));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        await dispatcher.DispatchAsync(command, cts.Token);

        // Assert
        capturedHandlerToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task DispatchAsync_WithToken_BehaviorPropagatesTokenToNextAsync()
    {
        // Arrange — behavior passes ct to next(), handler captures it
        CancellationToken capturedBehaviorToken = default;
        CancellationToken capturedHandlerToken = default;

        using var cts = new CancellationTokenSource();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>>(
                _ => new TokenCapturingHandler(ct => capturedHandlerToken = ct));
            services.AddTransient<ICommandBehavior<TestCommand, string>>(
                _ => new TokenCapturingBehavior(ct => capturedBehaviorToken = ct));
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        await dispatcher.DispatchAsync(command, cts.Token);

        // Assert — both should have the same token
        capturedBehaviorToken.Should().Be(cts.Token);
        capturedHandlerToken.Should().Be(cts.Token);
        capturedBehaviorToken.Should().Be(capturedHandlerToken);
    }

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
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

    #region Test Types

    internal sealed record TestCommand(string Value = "test") : ICommand<string>;

    internal sealed class TestHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
            => ValueTask.FromResult(command.Value);
    }

    internal sealed class TokenCapturingHandler(Action<CancellationToken> onCapture)
        : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            onCapture(ct);
            return ValueTask.FromResult(command.Value);
        }
    }

    internal sealed class TokenCapturingBehavior(Action<CancellationToken> onCapture)
        : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(
            TestCommand command,
            CommandHandlerDelegate<string> next,
            CancellationToken ct = default)
        {
            onCapture(ct);
            return await next(ct).ConfigureAwait(false);
        }
    }

    #endregion
}
