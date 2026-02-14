using Nexum.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore.Tests;

[Trait("Category", "Unit")]
public sealed class NexumProblemDetailsMapperTests
{
    [Fact]
    public void TryCreateProblemDetails_NexumHandlerNotFoundException_ReturnsTrueWithCorrectProblemDetails()
    {
        // Arrange
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");
        var options = new NexumProblemDetailsOptions();

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Handler Not Found");
        problemDetails.Type.Should().Be("/errors/handler-not-found");
        problemDetails.Detail.Should().Be(exception.Message);
        problemDetails.Extensions.Should().NotContainKey("exceptionMessage");
        problemDetails.Extensions.Should().NotContainKey("exceptionType");
        problemDetails.Extensions.Should().NotContainKey("stackTrace");
    }

    [Fact]
    public void TryCreateProblemDetails_NexumDispatchDepthExceededException_ReturnsTrueWithCorrectProblemDetails()
    {
        // Arrange
        var exception = new NexumDispatchDepthExceededException(16);
        var options = new NexumProblemDetailsOptions();

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Dispatch Depth Exceeded");
        problemDetails.Type.Should().Be("/errors/dispatch-depth-exceeded");
        problemDetails.Detail.Should().Be(exception.Message);
        problemDetails.Extensions.Should().NotContainKey("exceptionMessage");
        problemDetails.Extensions.Should().NotContainKey("exceptionType");
        problemDetails.Extensions.Should().NotContainKey("stackTrace");
    }

    [Fact]
    public void TryCreateProblemDetails_UnmappedException_ReturnsFalse()
    {
        // Arrange
        var exception = new ArgumentException("Test argument exception");
        var options = new NexumProblemDetailsOptions();

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeFalse();
        problemDetails.Should().BeNull();
    }

    [Fact]
    public void TryCreateProblemDetails_BaseTypeMatch_ReturnsTrueViaHierarchy()
    {
        // Arrange
        // NexumHandlerNotFoundException inherits from InvalidOperationException
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");
        var options = new NexumProblemDetailsOptions();

        // Remove exact type mapping, add base type mapping
        options.ExceptionMappings.Remove(typeof(NexumHandlerNotFoundException));
        options.ExceptionMappings[typeof(InvalidOperationException)] = static ex => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Operation",
            Type = "/errors/invalid-operation",
            Detail = ex.Message
        };

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Invalid Operation");
        problemDetails.Type.Should().Be("/errors/invalid-operation");
        problemDetails.Detail.Should().Be(exception.Message);
    }

    [Fact]
    public void TryCreateProblemDetails_FactoryReturnsNull_ReturnsFalse()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");
        var options = new NexumProblemDetailsOptions();

        // Add mapping that returns null
        options.ExceptionMappings[typeof(InvalidOperationException)] = static _ => null;

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeFalse();
        problemDetails.Should().BeNull();
    }

    [Fact]
    public void TryCreateProblemDetails_FactoryReturnsNullButBaseTypeMatches_ReturnsTrue()
    {
        // Arrange
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");
        var options = new NexumProblemDetailsOptions();

        // Override exact type mapping to return null, but base type (InvalidOperationException) has valid mapping
        options.ExceptionMappings[typeof(NexumHandlerNotFoundException)] = static _ => null;
        options.ExceptionMappings[typeof(InvalidOperationException)] = static ex => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Base Type Handler",
            Type = "/errors/base-type",
            Detail = ex.Message
        };

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status500InternalServerError);
        problemDetails.Title.Should().Be("Base Type Handler");
        problemDetails.Type.Should().Be("/errors/base-type");
        problemDetails.Detail.Should().Be(exception.Message);
    }

    [Fact]
    public void TryCreateProblemDetails_WithIncludeExceptionDetails_AddsExtensions()
    {
        // Arrange
        var exception = CreateExceptionWithStackTrace();
        var options = new NexumProblemDetailsOptions
        {
            IncludeExceptionDetails = true
        };

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Extensions.Should().ContainKey("exceptionMessage");
        problemDetails.Extensions["exceptionMessage"].Should().Be(exception.Message);
        problemDetails.Extensions.Should().ContainKey("exceptionType");
        problemDetails.Extensions["exceptionType"].Should().Be(exception.GetType().FullName);
        problemDetails.Extensions.Should().ContainKey("stackTrace");
        problemDetails.Extensions["stackTrace"].Should().Be(exception.StackTrace);
    }

    [Fact]
    public void TryCreateProblemDetails_WithoutIncludeExceptionDetails_NoExtensions()
    {
        // Arrange
        var exception = new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");
        var options = new NexumProblemDetailsOptions
        {
            IncludeExceptionDetails = false
        };

        // Act
        bool result = NexumProblemDetailsMapper.TryCreateProblemDetails(exception, options, out ProblemDetails? problemDetails);

        // Assert
        result.Should().BeTrue();
        problemDetails.Should().NotBeNull();
        problemDetails.Extensions.Should().NotContainKey("exceptionMessage");
        problemDetails.Extensions.Should().NotContainKey("exceptionType");
        problemDetails.Extensions.Should().NotContainKey("stackTrace");
    }

    // Helper method to create exception with stack trace
    private static NexumHandlerNotFoundException CreateExceptionWithStackTrace()
    {
        try
        {
            throw new NexumHandlerNotFoundException(typeof(TestCommand), "ICommandHandler");
        }
        catch (NexumHandlerNotFoundException ex)
        {
            return ex;
        }
    }

    // Test command for test purposes
    private sealed record TestCommand : ICommand<int>;
}
