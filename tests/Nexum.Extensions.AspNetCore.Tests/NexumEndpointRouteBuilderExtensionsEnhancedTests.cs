using System.Net;
using Nexum.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.AspNetCore.Tests;

/// <summary>
/// Tests for enhanced <see cref="NexumEndpointRouteBuilderExtensions"/> features:
/// endpoint naming, OpenAPI metadata, and Result mapping integration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NexumEndpointRouteBuilderExtensionsEnhancedTests
{
    [Fact]
    public async Task MapNexumCommand_WithResult_SetsEndpointNameAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();
        mockDispatcher
            .DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult("ok"));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<CreateOrderCommand, string>("/api/orders"));

        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act — verify the endpoint was registered with the correct name
        var endpointDataSource = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var endpoints = endpointDataSource.Endpoints;

        // Assert
        var endpoint = endpoints.FirstOrDefault(e =>
        {
            var metadata = e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>();
            return metadata != null && metadata.EndpointName == "CreateOrder";
        });
        endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task MapNexumCommand_Void_SetsEndpointNameAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<CancelOrderCommand>("/api/orders/cancel"));

        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var endpointDataSource = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var allEndpoints = endpointDataSource.Endpoints;

        // Assert
        var endpoint = allEndpoints.FirstOrDefault(e =>
        {
            var metadata = e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>();
            return metadata != null && metadata.EndpointName == "CancelOrder";
        });
        endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task MapNexumQuery_SetsEndpointNameAsync()
    {
        // Arrange
        IQueryDispatcher mockDispatcher = Substitute.For<IQueryDispatcher>();

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumQuery<GetOrderQuery, string>("/api/orders/{id}"));

        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var endpointDataSource = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var allEndpoints = endpointDataSource.Endpoints;

        // Assert
        var endpoint = allEndpoints.FirstOrDefault(e =>
        {
            var metadata = e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.EndpointNameMetadata>();
            return metadata != null && metadata.EndpointName == "GetOrder";
        });
        endpoint.Should().NotBeNull();
    }

    [Fact]
    public async Task WithNexumResultMapping_AddsFilterToEndpointAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints
                .MapNexumCommand<CreateOrderCommand, string>("/api/orders")
                .WithNexumResultMapping());

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act — make a request; the filter should be in the pipeline (even if no adapter registered)
        mockDispatcher
            .DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult("result-value"));

        using StringContent content = new("""{"Name":"test"}""", System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        // Assert — filter is transparent for non-Result types (passthrough)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static WebApplication CreateTestApp(
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureEndpoints = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddProblemDetails();
        builder.Services.AddNexumAspNetCore();
        configureServices?.Invoke(builder.Services);

        WebApplication app = builder.Build();
        app.UseNexum();
        configureEndpoints?.Invoke(app);
        return app;
    }

    // Test types
    private sealed record CreateOrderCommand(string Name) : ICommand<string>;
    private sealed record CancelOrderCommand(Guid OrderId) : IVoidCommand;
    private sealed record GetOrderQuery(Guid Id) : IQuery<string>;
}
