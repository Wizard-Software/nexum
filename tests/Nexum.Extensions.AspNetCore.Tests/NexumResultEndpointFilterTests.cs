using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;

namespace Nexum.Extensions.AspNetCore.Tests;

[Trait("Category", "Unit")]
public sealed class NexumResultEndpointFilterTests : IDisposable
{
    public NexumResultEndpointFilterTests()
    {
        NexumResultEndpointFilter.ResetCacheForTesting();
    }

    public void Dispose()
    {
        NexumResultEndpointFilter.ResetCacheForTesting();
    }

    [Fact]
    public async Task InvokeAsync_SuccessResult_ReturnsOkWithValueAsync()
    {
        // Arrange
        var adapter = new TestResultAdapter(isSuccess: true, value: "hello", error: null);
        ServiceProvider sp = BuildServiceProvider(adapter);
#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050
        var testResult = new TestResult(IsSuccessful: true, Value: "hello", Error: null);

        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>(testResult));

        // Assert
        result.Should().BeOfType<Ok<object>>();
        var ok = (Ok<object>)result!;
        ok.Value.Should().Be("hello");
    }

    [Fact]
    public async Task InvokeAsync_FailureResult_ReturnsProblemDetailsAsync()
    {
        // Arrange
        var adapter = new TestResultAdapter(isSuccess: false, value: null, error: "bad request");
        ServiceProvider sp = BuildServiceProvider(adapter);

#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050

        var testResult = new TestResult(IsSuccessful: false, Value: null, Error: "bad request");

        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>(testResult));

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
    }

    [Fact]
    public async Task InvokeAsync_NoAdapter_ReturnsResultAsIsAsync()
    {
        // Arrange — no adapter registered
        var services = new ServiceCollection();
        services.Configure<NexumEndpointOptions>(_ => { });
        ServiceProvider sp = services.BuildServiceProvider();
#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050
        var plainResult = new { Id = 42 };

        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>(plainResult));

        // Assert
        result.Should().BeSameAs(plainResult);
    }

    [Fact]
    public async Task InvokeAsync_NullResult_ReturnsNullAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.Configure<NexumEndpointOptions>(_ => { });
        ServiceProvider sp = services.BuildServiceProvider();
#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050
        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>((object?)null));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_FailureResultWithNullError_ReturnsOkWithResultAsync()
    {
        // Arrange
        var adapter = new TestResultAdapter(isSuccess: false, value: null, error: null);
        ServiceProvider sp = BuildServiceProvider(adapter);

#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050

        var testResult = new TestResult(IsSuccessful: false, Value: null, Error: null);

        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>(testResult));

        // Assert
        result.Should().BeOfType<Ok<object>>();
        var ok = (Ok<object>)result!;
        ok.Value.Should().BeSameAs(testResult);
    }

    [Fact]
    public async Task InvokeAsync_CustomErrorMapper_UsesCustomMappingAsync()
    {
        // Arrange
        var adapter = new TestResultAdapter(isSuccess: false, value: null, error: "validation error");
        var services = new ServiceCollection();
        services.AddSingleton<IResultAdapter<TestResult>>(adapter);
        services.Configure<NexumEndpointOptions>(options =>
        {
            options.ErrorToProblemDetails = error => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Custom Error",
                Detail = error.ToString()
            };
        });
        ServiceProvider sp = services.BuildServiceProvider();

#pragma warning disable IL3050
        var filter = new NexumResultEndpointFilter(sp);
#pragma warning restore IL3050

        var testResult = new TestResult(IsSuccessful: false, Value: null, Error: "validation error");

        // Act
        object? result = await filter.InvokeAsync(
            CreateContext(),
            _ => new ValueTask<object?>(testResult));

        // Assert
        result.Should().BeOfType<ProblemHttpResult>();
        var problemResult = (ProblemHttpResult)result!;
        problemResult.StatusCode.Should().Be(422);
        problemResult.ProblemDetails.Title.Should().Be("Custom Error");
    }

    private static ServiceProvider BuildServiceProvider(TestResultAdapter adapter)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IResultAdapter<TestResult>>(adapter);
        services.Configure<NexumEndpointOptions>(_ => { });
        return services.BuildServiceProvider();
    }

    private static DefaultEndpointFilterInvocationContext CreateContext()
    {
        // Create a minimal HttpContext for the filter
        var httpContext = new DefaultHttpContext();
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    // Test types
    private sealed record TestResult(bool IsSuccessful, object? Value, object? Error);

    private sealed class TestResultAdapter(bool isSuccess, object? value, object? error) : IResultAdapter<TestResult>
    {
        public bool IsSuccess(TestResult result)
        {
            return isSuccess;
        }

        public object? GetValue(TestResult result)
        {
            return value;
        }

        public object? GetError(TestResult result)
        {
            return error;
        }
    }
}
