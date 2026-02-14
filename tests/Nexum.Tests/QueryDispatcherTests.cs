using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class QueryDispatcherTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task DispatchAsync_WithRegisteredHandler_ReturnsResultAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, string>, TestQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("hello");

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task DispatchAsync_WithoutHandler_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(); // No handler registered
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("test");

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<NexumHandlerNotFoundException>()
            .WithMessage("*No handler registered for*");
    }

    [Fact]
    public async Task DispatchAsync_ExceedingMaxDepth_ThrowsNexumDispatchDepthExceededExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        services.AddScoped<IQueryHandler<ReentrantQuery, string>, ReentrantQueryHandler>();

        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new ReentrantQuery();

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);
        var exception = await act.Should().ThrowAsync<NexumDispatchDepthExceededException>();
        exception.Which.MaxDepth.Should().Be(1);
    }

    [Fact]
    public async Task DispatchAsync_WhenHandlerThrows_InvokesExceptionHandlerAndRethrowsAsync()
    {
        // Arrange
        var exceptionHandlerInvoked = false;

        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, string>, ThrowingQueryHandler>();
            services.AddScoped<IQueryExceptionHandler<TestQuery, InvalidOperationException>>(_ =>
                new TestQueryExceptionHandler(() => exceptionHandlerInvoked = true));
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("test");

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");

        exceptionHandlerInvoked.Should().BeTrue("exception handler should be invoked before re-throwing");
    }

    [Fact]
    public async Task DispatchAsync_NullQuery_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync<string>(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DispatchAsync_CancellationRequested_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel to test propagation

        var capturingHandler = new CapturingQueryHandler();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<TestQuery, string>>(_ => capturingHandler);
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("test");

        // Act
        await dispatcher.DispatchAsync(query, cts.Token);

        // Assert
        capturingHandler.CapturedToken.Should().Be(cts.Token);
        capturingHandler.CapturedToken.IsCancellationRequested.Should().BeTrue();
    }

    #region Helper Methods

    private static ServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    #endregion

    #region Test Types

    internal sealed record TestQuery(string Value = "test") : IQuery<string>;

    internal sealed record ReentrantQuery : IQuery<string>;

    internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
    {
        public ValueTask<string> HandleAsync(TestQuery query, CancellationToken ct = default)
        {
            return ValueTask.FromResult(query.Value);
        }
    }

    internal sealed class ThrowingQueryHandler : IQueryHandler<TestQuery, string>
    {
        public ValueTask<string> HandleAsync(TestQuery query, CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    internal sealed class CapturingQueryHandler : IQueryHandler<TestQuery, string>
    {
        public CancellationToken CapturedToken { get; private set; }

        public ValueTask<string> HandleAsync(TestQuery query, CancellationToken ct = default)
        {
            CapturedToken = ct;
            return ValueTask.FromResult(query.Value);
        }
    }

    internal sealed class ReentrantQueryHandler(IServiceProvider serviceProvider)
        : IQueryHandler<ReentrantQuery, string>
    {
        public async ValueTask<string> HandleAsync(ReentrantQuery query, CancellationToken ct = default)
        {
            var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();
            return await dispatcher.DispatchAsync(query, ct);
        }
    }

    internal sealed class TestQueryExceptionHandler(Action onInvoked)
        : IQueryExceptionHandler<TestQuery, InvalidOperationException>
    {
        public ValueTask HandleAsync(
            TestQuery query,
            InvalidOperationException exception,
            CancellationToken ct = default)
        {
            onInvoked();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
