using System.Net;
using System.Net.Http.Json;
using Nexum.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.AspNetCore.Tests;

/// <summary>
/// Integration tests for <see cref="NexumMiddleware"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class NexumMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NexumHandlerNotFoundException_Returns500ProblemDetailsAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(configureEndpoints: endpoints =>
        {
            endpoints.MapGet("/test", () =>
            {
                throw new NexumHandlerNotFoundException(typeof(string), "ICommandHandler");
            });
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(500);
        problemDetails.Title.Should().Be("Handler Not Found");
        problemDetails.Type.Should().Be("/errors/handler-not-found");
        problemDetails.Detail.Should().Contain("No handler registered for String");
    }

    [Fact]
    public async Task InvokeAsync_NexumDispatchDepthExceededException_Returns500ProblemDetailsAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(configureEndpoints: endpoints =>
        {
            endpoints.MapGet("/test", () =>
            {
                throw new NexumDispatchDepthExceededException(16);
            });
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(500);
        problemDetails.Title.Should().Be("Dispatch Depth Exceeded");
        problemDetails.Type.Should().Be("/errors/dispatch-depth-exceeded");
        problemDetails.Detail.Should().Contain("Dispatch depth exceeded maximum of 16");
    }

    [Fact]
    public async Task InvokeAsync_NonNexumException_RethrowsAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(configureEndpoints: endpoints =>
        {
            endpoints.MapGet("/test", () =>
            {
                throw new InvalidOperationException("Not a Nexum exception");
            });
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        // Non-Nexum exceptions are re-thrown and handled by ASP.NET Core's default exception handler
        // which returns 500 with a generic response (not ProblemDetails unless AddProblemDetails handles it)
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        // The content should NOT be application/problem+json for unmapped exceptions
        // (unless AddProblemDetails has a default handler, but we're testing middleware behavior)
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (contentType == "application/problem+json")
        {
            // If AddProblemDetails handled it, verify it's not a Nexum-mapped ProblemDetails
            ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
            problemDetails?.Title.Should().NotBe("Handler Not Found");
            problemDetails?.Title.Should().NotBe("Dispatch Depth Exceeded");
        }
    }

    [Fact]
    public async Task InvokeAsync_WithoutProblemDetailsService_RethrowsAsync()
    {
        // Arrange
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        // NOTE: NOT calling AddProblemDetails() — IProblemDetailsService will be unavailable
        builder.Services.AddNexumAspNetCore();

        await using WebApplication app = builder.Build();
        app.UseNexum();
        app.MapGet("/test", () =>
        {
            throw new NexumHandlerNotFoundException(typeof(string), "ICommandHandler");
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        // Middleware should re-throw when IProblemDetailsService is missing, resulting in 500 error
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PassesThroughAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(configureEndpoints: endpoints =>
        {
            endpoints.MapGet("/test", () => Results.Ok(new { Message = "Success" }));
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        string content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content.Should().Contain("Success");
    }

    [Fact]
    public async Task InvokeAsync_DispatcherThrowsNexumHandlerNotFoundException_Returns500ProblemDetailsAsync()
    {
        // Arrange
        ICommandDispatcher mockDispatcher = Substitute.For<ICommandDispatcher>();
        mockDispatcher
            .DispatchAsync(Arg.Any<ICommand<string>>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler"));

        await using WebApplication app = CreateTestApp(
            configureServices: services => services.AddSingleton(mockDispatcher),
            configureEndpoints: endpoints => endpoints.MapNexumCommand<TestCommand, string>("/api/test"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        const string TestPayload = """{"Name":"test"}""";
        using StringContent content = new(TestPayload, System.Text.Encoding.UTF8, "application/json");
        HttpResponseMessage response = await client.PostAsync("/api/test", content, TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(500);
        problemDetails.Title.Should().Be("Handler Not Found");
    }

    [Fact]
    public async Task InvokeAsync_CustomExceptionMapping_ReturnsCustomProblemDetailsAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(
            configureServices: services =>
            {
                services.Configure<NexumProblemDetailsOptions>(o =>
                {
                    o.ExceptionMappings[typeof(CustomTestException)] = static ex => new ProblemDetails
                    {
                        Status = StatusCodes.Status422UnprocessableEntity,
                        Title = "Custom Error",
                        Type = "/errors/custom",
                        Detail = ex.Message
                    };
                });
            },
            configureEndpoints: endpoints =>
            {
                endpoints.MapGet("/test", () =>
                {
                    throw new CustomTestException("custom error");
                });
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(422);
        problemDetails.Title.Should().Be("Custom Error");
        problemDetails.Type.Should().Be("/errors/custom");
        problemDetails.Detail.Should().Contain("custom error");
    }

    [Fact]
    public async Task InvokeAsync_IncludeExceptionDetails_ReturnsExtensionsInResponseAsync()
    {
        // Arrange
        await using WebApplication app = CreateTestApp(
            configureServices: services =>
            {
                services.Configure<NexumProblemDetailsOptions>(o =>
                {
                    o.IncludeExceptionDetails = true;
                });
            },
            configureEndpoints: endpoints =>
            {
                endpoints.MapGet("/test", () =>
                {
                    throw new NexumHandlerNotFoundException(typeof(string), "ICommandHandler");
                });
            });

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act
        HttpResponseMessage response = await client.GetAsync("/test", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        ProblemDetails? problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(500);
        problemDetails.Extensions.Should().ContainKey("exceptionMessage");
        problemDetails.Extensions.Should().ContainKey("exceptionType");
        problemDetails.Extensions.Should().ContainKey("stackTrace");
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
    private sealed record TestCommand(string Name) : ICommand<string>;

    private sealed class CustomTestException(string message) : Exception(message);
}
