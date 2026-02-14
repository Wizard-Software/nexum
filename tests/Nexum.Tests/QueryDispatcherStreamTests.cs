using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class QueryDispatcherStreamTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task StreamAsync_WithRegisteredHandler_ReturnsStreamAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton<IStreamQueryHandler<TestStreamQuery, int>, TestStreamQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();

        // Act
        var results = new List<int>();
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert
        results.Should().ContainInOrder(1, 2, 3);
        results.Count.Should().Be(3);
    }

    [Fact]
    public void StreamAsync_WithoutHandler_ThrowsInvalidOperationExceptionImmediatelyAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();

        // Act - sync lambda because StreamAsync throws immediately, not during iteration
        var act = () => dispatcher.StreamAsync(query);

        // Assert - sync assertion because throw is immediate
        act.Should().Throw<NexumHandlerNotFoundException>()
            .WithMessage("*No handler registered for*TestStreamQuery*");
    }

    [Fact]
    public void StreamAsync_NullQuery_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        // Act - sync lambda because StreamAsync throws immediately
        var act = () => dispatcher.StreamAsync<int>(null!);

        // Assert - sync assertion because throw is immediate
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("query");
    }

    [Fact]
    public void StreamAsync_ExceedingMaxDepth_ThrowsNexumDispatchDepthExceededExceptionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(new NexumOptions { MaxDispatchDepth = 1 });
            services.AddSingleton<IStreamQueryHandler<TestStreamQuery, int>, TestStreamQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();

        // Enter depth guard manually to simulate being at max depth
        using var guard = DispatchDepthGuard.Enter(1); // depth is now 1

        // Act - StreamAsync should throw immediately because depth would exceed max
        var act = () => dispatcher.StreamAsync(query);

        // Assert - sync assertion because throw happens during setup, not iteration
        act.Should().Throw<NexumDispatchDepthExceededException>()
            .WithMessage("Dispatch depth exceeded maximum of 1*");
    }

    [Fact]
    public async Task StreamAsync_WithCancellation_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton<IStreamQueryHandler<TestStreamQuery, int>, CapturingStreamQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();
        var cts = new CancellationTokenSource();
        var handler = sp.GetRequiredService<IStreamQueryHandler<TestStreamQuery, int>>() as CapturingStreamQueryHandler;

        // Act
        await foreach (var _ in dispatcher.StreamAsync(query, cts.Token))
        {
            break; // consume one item
        }

        // Assert
        handler!.CapturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task StreamAsync_WithBehavior_BehaviorInterceptsStreamAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton<IStreamQueryHandler<TestStreamQuery, int>, TestStreamQueryHandler>();
            services.AddSingleton<IStreamQueryBehavior<TestStreamQuery, int>, InterceptingStreamBehavior>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();

        // Act
        var results = new List<int>();
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert - behavior prepends 0, handler yields 1, 2, 3
        results.Should().ContainInOrder(0, 1, 2, 3);
        results.Count.Should().Be(4);
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

    internal sealed record TestStreamQuery : IStreamQuery<int>;

    internal sealed class TestStreamQueryHandler : IStreamQueryHandler<TestStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamQuery query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield(); // Make async for proper cancellation token handling
            yield return 1;
            yield return 2;
            yield return 3;
        }
    }

    internal sealed class CapturingStreamQueryHandler : IStreamQueryHandler<TestStreamQuery, int>
    {
        public CancellationToken CapturedToken { get; private set; }

        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamQuery query,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            await Task.Yield();
            yield return 1;
        }
    }

    internal sealed class InterceptingStreamBehavior : IStreamQueryBehavior<TestStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamQuery query,
            StreamQueryHandlerDelegate<int> next,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Prepend 0 before delegating to next
            yield return 0;

            await foreach (var item in next(cancellationToken))
            {
                yield return item;
            }
        }
    }

    #endregion
}
