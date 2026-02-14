using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Integration")]
public sealed class PolymorphicQueryResolutionIntegrationTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task DispatchAsync_DerivedQuery_ResolvesToBaseHandlerAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<BaseQuery, string>, BaseQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query = new DerivedQuery();

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("base-handler-result");
    }

    [Fact]
    public async Task DispatchAsync_DerivedQuery_SecondCall_UsesCachedResolutionAsync()
    {
        // Arrange
        using var sp = CreateServiceProvider(services =>
        {
            services.AddScoped<IQueryHandler<BaseQuery, string>, BaseQueryHandler>();
        });

        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();
        var query1 = new DerivedQuery();
        var query2 = new DerivedQuery();

        // Act
        var result1 = await dispatcher.DispatchAsync(query1, TestContext.Current.CancellationToken);
        var result2 = await dispatcher.DispatchAsync(query2, TestContext.Current.CancellationToken);

        // Assert
        result1.Should().Be("base-handler-result");
        result2.Should().Be("base-handler-result");
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

    internal abstract class BaseQuery : IQuery<string>;

    internal sealed class DerivedQuery : BaseQuery;

    internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
    {
        public ValueTask<string> HandleAsync(BaseQuery query, CancellationToken ct = default)
        {
            return ValueTask.FromResult("base-handler-result");
        }
    }

    #endregion
}
