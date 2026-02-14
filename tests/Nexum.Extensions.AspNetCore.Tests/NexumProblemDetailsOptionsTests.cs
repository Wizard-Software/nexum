using Nexum.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore.Tests;

[Trait("Category", "Unit")]
public sealed class NexumProblemDetailsOptionsTests
{
    [Fact]
    public void ExceptionMappings_Default_ContainsNexumHandlerNotFoundExceptionMapping()
    {
        // Arrange
        var options = new NexumProblemDetailsOptions();
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");

        // Act
        bool hasMappingKey = options.ExceptionMappings.ContainsKey(typeof(NexumHandlerNotFoundException));
        ProblemDetails? problemDetails = options.ExceptionMappings[typeof(NexumHandlerNotFoundException)](exception);

        // Assert
        hasMappingKey.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Handler Not Found");
        problemDetails.Type.Should().Be("/errors/handler-not-found");
        problemDetails.Detail.Should().Be(exception.Message);
    }

    [Fact]
    public void ExceptionMappings_Default_ContainsNexumDispatchDepthExceededExceptionMapping()
    {
        // Arrange
        var options = new NexumProblemDetailsOptions();
        var exception = new NexumDispatchDepthExceededException(16);

        // Act
        bool hasMappingKey = options.ExceptionMappings.ContainsKey(typeof(NexumDispatchDepthExceededException));
        ProblemDetails? problemDetails = options.ExceptionMappings[typeof(NexumDispatchDepthExceededException)](exception);

        // Assert
        hasMappingKey.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Dispatch Depth Exceeded");
        problemDetails.Type.Should().Be("/errors/dispatch-depth-exceeded");
        problemDetails.Detail.Should().Be(exception.Message);
    }

    [Fact]
    public void IncludeExceptionDetails_Default_IsFalse()
    {
        // Arrange & Act
        var options = new NexumProblemDetailsOptions();

        // Assert
        options.IncludeExceptionDetails.Should().BeFalse();
    }

    [Fact]
    public void ExceptionMappings_CustomMapping_CanBeAdded()
    {
        // Arrange
        var options = new NexumProblemDetailsOptions();
        var exception = new CustomTestException("Custom error");

        // Act
        options.ExceptionMappings[typeof(CustomTestException)] = static ex => new ProblemDetails
        {
            Status = StatusCodes.Status418ImATeapot,
            Title = "Custom Error",
            Type = "/errors/custom",
            Detail = ex.Message
        };

        bool hasMappingKey = options.ExceptionMappings.ContainsKey(typeof(CustomTestException));
        ProblemDetails? problemDetails = options.ExceptionMappings[typeof(CustomTestException)](exception);

        // Assert
        hasMappingKey.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status418ImATeapot);
        problemDetails.Title.Should().Be("Custom Error");
        problemDetails.Type.Should().Be("/errors/custom");
        problemDetails.Detail.Should().Be("Custom error");
    }

    [Fact]
    public void ExceptionMappings_OverrideDefault_ReplacesExistingMapping()
    {
        // Arrange
        var options = new NexumProblemDetailsOptions();
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");

        const int CustomStatus = StatusCodes.Status503ServiceUnavailable;
        const string CustomTitle = "Custom Handler Not Found";
        const string CustomType = "/errors/custom-handler-not-found";

        // Act
        options.ExceptionMappings[typeof(NexumHandlerNotFoundException)] = static ex => new ProblemDetails
        {
            Status = CustomStatus,
            Title = CustomTitle,
            Type = CustomType,
            Detail = ex.Message
        };

        ProblemDetails? problemDetails = options.ExceptionMappings[typeof(NexumHandlerNotFoundException)](exception);

        // Assert
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(CustomStatus);
        problemDetails.Title.Should().Be(CustomTitle);
        problemDetails.Type.Should().Be(CustomType);
        problemDetails.Detail.Should().Be(exception.Message);
    }

    // Test types
    private sealed record TestCommand : ICommand<int>;

    private sealed class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
}
