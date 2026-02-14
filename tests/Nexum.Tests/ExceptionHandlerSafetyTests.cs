using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

/// <summary>
/// Tests that exception handlers which throw during execution are safely handled:
/// original exception is propagated, handler failure is logged as warning.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExceptionHandlerSafetyTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task ExceptionHandler_ThrowsDuringHandle_OriginalExceptionPropagatedAsync()
    {
        // Arrange — register a handler that throws, and an exception handler that ALSO throws
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<ICommandHandler<TestCommand, string>, ThrowingHandler>();
            services.AddTransient<ICommandExceptionHandler<TestCommand, ArgumentException>,
                ThrowingExceptionHandler>();
        });

        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        var act = async () => await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert — the ORIGINAL exception (ArgumentException from handler) must be propagated,
        // not the InvalidOperationException from the exception handler
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Original handler exception");
    }

    [Fact]
    public async Task ExceptionHandler_ThrowsDuringHandle_WarningLoggedAsync()
    {
        // Arrange
        var logMessages = new List<(LogLevel Level, string Message)>();
        var testLogger = new TrackingLogger(logMessages);

        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(testLogger);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<ICommandHandler<TestCommand, string>, ThrowingHandler>();
        services.AddTransient<ICommandExceptionHandler<TestCommand, ArgumentException>,
            ThrowingExceptionHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var command = new TestCommand("test");

        // Act
        try
        {
            await dispatcher.DispatchAsync(command, CancellationToken.None);
        }
        catch (ArgumentException)
        {
            // Expected — original exception re-thrown
        }

        // Assert — exception handler failure should be logged as Warning (not Error!)
        logMessages.Should().ContainSingle(m => m.Level == LogLevel.Warning);
    }

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    #region Test Types

    internal sealed record TestCommand(string Value = "test") : ICommand<string>;

    internal sealed class ThrowingHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            throw new ArgumentException("Original handler exception");
        }
    }

    /// <summary>
    /// An exception handler that itself throws — used to test safety of exception handler invocation.
    /// </summary>
    internal sealed class ThrowingExceptionHandler : ICommandExceptionHandler<TestCommand, ArgumentException>
    {
        public ValueTask HandleAsync(TestCommand command, ArgumentException exception, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Exception handler blew up");
        }
    }

    /// <summary>
    /// A minimal ILogger that tracks log entries for assertions.
    /// </summary>
    private sealed class TrackingLogger(List<(LogLevel Level, string Message)> entries)
        : ILogger<ExceptionHandlerResolver>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add((logLevel, formatter(state, exception)));
        }
    }

    #endregion
}
