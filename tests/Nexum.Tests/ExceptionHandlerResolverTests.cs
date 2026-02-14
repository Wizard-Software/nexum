using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class ExceptionHandlerResolverTests
{
    [Fact]
    public async Task InvokeQueryExceptionHandlersAsync_ExactMatch_InvokesHandlerAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<IQueryExceptionHandler<TestQuery, InvalidOperationException>>(_ =>
            new TrackingQueryExceptionHandler<TestQuery, InvalidOperationException>(tracker, "TestQuery-InvalidOperationException"));

        var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var query = new TestQuery("test");
        var exception = new InvalidOperationException("test error");

        // Act
        await resolver.InvokeQueryExceptionHandlersAsync(query, exception, CancellationToken.None);

        // Assert
        tracker.Should().BeEquivalentTo(["TestQuery-InvalidOperationException"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InvokeCommandExceptionHandlersAsync_BaseCommandAxis_FallsBackToBaseHandlerAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<ICommandExceptionHandler<BaseCommand, InvalidOperationException>>(_ =>
            new TrackingCommandExceptionHandler<BaseCommand, InvalidOperationException>(tracker, "BaseCommand-InvalidOperationException"));

        var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var command = new DerivedCommand();
        var exception = new InvalidOperationException("test error");

        // Act
        await resolver.InvokeCommandExceptionHandlersAsync(command, exception, CancellationToken.None);

        // Assert
        tracker.Should().BeEquivalentTo(["BaseCommand-InvalidOperationException"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InvokeCommandExceptionHandlersAsync_BaseExceptionAxis_FallsBackToBaseExceptionAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<ICommandExceptionHandler<TestCommand, Exception>>(_ =>
            new TrackingCommandExceptionHandler<TestCommand, Exception>(tracker, "TestCommand-Exception"));

        var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var command = new TestCommand("test");
        var exception = new CustomException("test error");

        // Act
        await resolver.InvokeCommandExceptionHandlersAsync(command, exception, CancellationToken.None);

        // Assert
        tracker.Should().BeEquivalentTo(["TestCommand-Exception"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InvokeCommandExceptionHandlersAsync_TwoAxisHierarchy_CommandAxisFirstAsync()
    {
        // Arrange
        var tracker = new List<string>();
        var services = new ServiceCollection();

        // Register handlers in the two-axis hierarchy
        services.AddTransient<ICommandExceptionHandler<DerivedCommand, CustomException>>(_ =>
            new TrackingCommandExceptionHandler<DerivedCommand, CustomException>(tracker, "DerivedCommand-CustomException"));
        services.AddTransient<ICommandExceptionHandler<DerivedCommand, InvalidOperationException>>(_ =>
            new TrackingCommandExceptionHandler<DerivedCommand, InvalidOperationException>(tracker, "DerivedCommand-InvalidOperationException"));
        services.AddTransient<ICommandExceptionHandler<DerivedCommand, Exception>>(_ =>
            new TrackingCommandExceptionHandler<DerivedCommand, Exception>(tracker, "DerivedCommand-Exception"));
        services.AddTransient<ICommandExceptionHandler<BaseCommand, CustomException>>(_ =>
            new TrackingCommandExceptionHandler<BaseCommand, CustomException>(tracker, "BaseCommand-CustomException"));
        services.AddTransient<ICommandExceptionHandler<BaseCommand, InvalidOperationException>>(_ =>
            new TrackingCommandExceptionHandler<BaseCommand, InvalidOperationException>(tracker, "BaseCommand-InvalidOperationException"));
        services.AddTransient<ICommandExceptionHandler<BaseCommand, Exception>>(_ =>
            new TrackingCommandExceptionHandler<BaseCommand, Exception>(tracker, "BaseCommand-Exception"));

        var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var command = new DerivedCommand();
        var exception = new CustomException("test error");

        // Act
        await resolver.InvokeCommandExceptionHandlersAsync(command, exception, CancellationToken.None);

        // Assert
        // Outer loop: DerivedCommand → BaseCommand → ICommand
        // Inner loop: CustomException → InvalidOperationException → Exception
        tracker.Should().BeEquivalentTo([
            "DerivedCommand-CustomException",
            "DerivedCommand-InvalidOperationException",
            "DerivedCommand-Exception",
            "BaseCommand-CustomException",
            "BaseCommand-InvalidOperationException",
            "BaseCommand-Exception"
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task InvokeQueryExceptionHandlersAsync_NoHandlers_CompletesWithoutErrorAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var resolver = CreateResolver(provider);

        var query = new TestQuery("test");
        var exception = new InvalidOperationException("test error");

        // Act
        var act = async () => await resolver.InvokeQueryExceptionHandlersAsync(query, exception, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    private static ExceptionHandlerResolver CreateResolver(IServiceProvider sp)
    {
        return new ExceptionHandlerResolver(
            sp,
            NullLogger<ExceptionHandlerResolver>.Instance);
    }

    #region Test Types

    // Command types
    internal sealed record TestCommand(string Value = "test") : ICommand<string>;

    internal abstract class BaseCommand : ICommand<string>;

    internal sealed class DerivedCommand : BaseCommand;

    // Query types
    internal sealed record TestQuery(string Value = "test") : IQuery<string>;

    // Exception types
    internal sealed class CustomException : InvalidOperationException
    {
        public CustomException(string message) : base(message) { }
    }

    // Tracking exception handlers
    internal sealed class TrackingCommandExceptionHandler<TCommand, TException>(List<string> tracker, string label)
        : ICommandExceptionHandler<TCommand, TException>
        where TCommand : ICommand
        where TException : Exception
    {
        public ValueTask HandleAsync(TCommand command, TException exception, CancellationToken ct = default)
        {
            tracker.Add(label);
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class TrackingQueryExceptionHandler<TQuery, TException>(List<string> tracker, string label)
        : IQueryExceptionHandler<TQuery, TException>
        where TQuery : IQuery
        where TException : Exception
    {
        public ValueTask HandleAsync(TQuery query, TException exception, CancellationToken ct = default)
        {
            tracker.Add(label);
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
