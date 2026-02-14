using System.Runtime.CompilerServices;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class QueryPipelineIntegrationTests : IDisposable
{
    [Fact]
    public async Task QueryDispatch_WithBehaviors_ExecutesInCorrectOrderAsync()
    {
        // Arrange
        var tracker = new List<string>();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(tracker);
            services.AddScoped<IQueryHandler<TestQuery, string>, TestHandler>();
            services.AddTransient<IQueryBehavior<TestQuery, string>, TrackingQueryBehavior1>();
            services.AddTransient<IQueryBehavior<TestQuery, string>, TrackingQueryBehavior2>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("test-value");

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("test-value");
        tracker.Should().BeEquivalentTo(
            ["Behavior1:Before", "Behavior2:Before", "Handler", "Behavior2:After", "Behavior1:After"],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task StreamQuery_WithBehaviors_ExecutesInCorrectOrderAsync()
    {
        // Arrange
        var tracker = new List<string>();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(tracker);
            services.AddScoped<IStreamQueryHandler<TestStreamQuery, int>, TestStreamHandler>();
            services.AddTransient<IStreamQueryBehavior<TestStreamQuery, int>, TrackingStreamBehavior1>();
            services.AddTransient<IStreamQueryBehavior<TestStreamQuery, int>, TrackingStreamBehavior2>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestStreamQuery();

        // Act
        var items = new List<int>();
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert
        items.Should().BeEquivalentTo([1, 2, 3], options => options.WithStrictOrdering());
        tracker.Should().BeEquivalentTo(
            ["StreamBehavior1:Before", "StreamBehavior2:Before", "Handler", "StreamBehavior2:After", "StreamBehavior1:After"],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task QueryDispatch_NoBehaviors_HandlerExecutedDirectlyAsync()
    {
        // Arrange
        var tracker = new List<string>();
        using var sp = CreateServiceProvider(services =>
        {
            services.AddSingleton(tracker);
            services.AddScoped<IQueryHandler<TestQuery, string>, TestHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new TestQuery("direct-value");

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("direct-value");
        tracker.Should().BeEquivalentTo(["Handler"], options => options.WithStrictOrdering());
    }

    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

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

    // Test types
    private sealed record TestQuery(string Value) : IQuery<string>;
    private sealed record TestStreamQuery : IStreamQuery<int>;

    private sealed class TestHandler(List<string> tracker) : IQueryHandler<TestQuery, string>
    {
        public ValueTask<string> HandleAsync(TestQuery query, CancellationToken ct = default)
        {
            tracker.Add("Handler");
            return new ValueTask<string>(query.Value);
        }
    }

    private sealed class TestStreamHandler(List<string> tracker) : IStreamQueryHandler<TestStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(TestStreamQuery query, [EnumeratorCancellation] CancellationToken ct = default)
        {
            tracker.Add("Handler");
            yield return 1;
            yield return 2;
            yield return 3;
            await Task.CompletedTask.ConfigureAwait(false);
        }
    }

    [BehaviorOrder(1)]
    private sealed class TrackingQueryBehavior1(List<string> tracker) : IQueryBehavior<TestQuery, string>
    {
        public async ValueTask<string> HandleAsync(TestQuery query, QueryHandlerDelegate<string> next, CancellationToken ct = default)
        {
            tracker.Add("Behavior1:Before");
            var result = await next(ct).ConfigureAwait(false);
            tracker.Add("Behavior1:After");
            return result;
        }
    }

    [BehaviorOrder(2)]
    private sealed class TrackingQueryBehavior2(List<string> tracker) : IQueryBehavior<TestQuery, string>
    {
        public async ValueTask<string> HandleAsync(TestQuery query, QueryHandlerDelegate<string> next, CancellationToken ct = default)
        {
            tracker.Add("Behavior2:Before");
            var result = await next(ct).ConfigureAwait(false);
            tracker.Add("Behavior2:After");
            return result;
        }
    }

    [BehaviorOrder(1)]
    private sealed class TrackingStreamBehavior1(List<string> tracker) : IStreamQueryBehavior<TestStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamQuery query,
            StreamQueryHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            tracker.Add("StreamBehavior1:Before");
            await foreach (var item in next(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                yield return item;
            }
            tracker.Add("StreamBehavior1:After");
        }
    }

    [BehaviorOrder(2)]
    private sealed class TrackingStreamBehavior2(List<string> tracker) : IStreamQueryBehavior<TestStreamQuery, int>
    {
        public async IAsyncEnumerable<int> HandleAsync(
            TestStreamQuery query,
            StreamQueryHandlerDelegate<int> next,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            tracker.Add("StreamBehavior2:Before");
            await foreach (var item in next(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                yield return item;
            }
            tracker.Add("StreamBehavior2:After");
        }
    }
}
