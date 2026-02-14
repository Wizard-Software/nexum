using System.Net;
using System.Net.Http.Json;
using Nexum.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.AspNetCore.Tests;

/// <summary>
/// Integration tests for <see cref="NexumEndpointRouteBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NexumEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public async Task MapNexumCommand_WithResult_DispatchesAndReturns200OkAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();
        mockDispatcher.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult("command-result"));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<TestCommand, string>("/api/test-command"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        TestCommand command = new("test-name");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/test-command", command, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string? result = await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
        result.Should().Be("command-result");

        await mockDispatcher.Received(1).DispatchAsync(
            Arg.Is<ICommand<string>>(c => c.GetType() == typeof(TestCommand) && ((TestCommand)c).Name == "test-name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MapNexumCommand_VoidCommand_DispatchesAndReturns204NoContentAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();
        mockDispatcher.DispatchAsync(Arg.Any<ICommand<Unit>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(Unit.Value));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<TestVoidCommand>("/api/test-void-command"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        TestVoidCommand command = new("test-name");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/test-void-command", command, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Length.Should().Be(0);

        await mockDispatcher.Received(1).DispatchAsync(
            Arg.Is<ICommand<Unit>>(c => c.GetType() == typeof(TestVoidCommand) && ((TestVoidCommand)c).Name == "test-name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MapNexumQuery_WithResult_DispatchesAndReturns200OkAsync()
    {
        // Arrange
        IQueryDispatcher mockDispatcher = Substitute.For<IQueryDispatcher>();
        mockDispatcher.DispatchAsync(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult("query-result"));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumQuery<TestQuery, string>("/api/test-query"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/test-query?name=test-name", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        string? result = await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);
        result.Should().Be("query-result");

        await mockDispatcher.Received(1).DispatchAsync(
            Arg.Is<IQuery<string>>(q => q.GetType() == typeof(TestQuery) && ((TestQuery)q).Name == "test-name"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MapNexumCommand_DispatcherThrows_ExceptionPropagatesAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();
        mockDispatcher.DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<string>>(x => throw new InvalidOperationException("Dispatcher error"));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<TestCommand, string>("/api/test"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        TestCommand command = new("test");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/test", command, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // Verify this is NOT a Nexum-specific ProblemDetails response
        // (non-Nexum exceptions are not mapped by NexumMiddleware)
        string? contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType == "application/problem+json")
        {
            // ASP.NET Core's default ProblemDetails handler may still create a response
            // but it should NOT contain Nexum-specific titles
            ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
            problemDetails?.Title.Should().NotBe("Handler Not Found");
            problemDetails?.Title.Should().NotBe("Dispatch Depth Exceeded");
        }
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

    /// <summary>Test command with result.</summary>
    internal sealed record TestCommand(string Name) : ICommand<string>;

    /// <summary>Test void command.</summary>
    internal sealed record TestVoidCommand(string Name) : IVoidCommand;

    /// <summary>Test query with result.</summary>
    internal sealed record TestQuery : IQuery<string>
    {
        /// <summary>Query name from query string.</summary>
        [FromQuery]
        public string Name { get; init; } = "";
    }
}
