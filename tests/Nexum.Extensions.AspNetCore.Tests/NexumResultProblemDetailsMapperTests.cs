using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore.Tests;

[Trait("Category", "Unit")]
public sealed class NexumResultProblemDetailsMapperTests
{
    [Fact]
    public void CreateProblemDetails_DefaultMapping_ReturnsProblemDetailsWithFailureStatusCode()
    {
        // Arrange
        var options = new NexumEndpointOptions { FailureStatusCode = StatusCodes.Status400BadRequest };
        var error = new { Message = "Something went wrong" };

        // Act
        ProblemDetails result = NexumResultProblemDetailsMapper.CreateProblemDetails(error, options);

        // Assert
        result.Status.Should().Be(400);
        result.Title.Should().Be("Request Failed");
        result.Type.Should().Be("/errors/status-400");
        result.Detail.Should().Contain("Something went wrong");
    }

    [Fact]
    public void CreateProblemDetails_CustomMapper_ReturnsCustomProblemDetails()
    {
        // Arrange
        var options = new NexumEndpointOptions
        {
            ErrorToProblemDetails = error => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation Failed",
                Detail = error.ToString()
            }
        };

        // Act
        ProblemDetails result = NexumResultProblemDetailsMapper.CreateProblemDetails("validation error", options);

        // Assert
        result.Status.Should().Be(422);
        result.Title.Should().Be("Validation Failed");
    }

    [Fact]
    public void CreateProblemDetails_CustomMapperReturnsNull_FallsBackToDefault()
    {
        // Arrange
        var options = new NexumEndpointOptions
        {
            FailureStatusCode = StatusCodes.Status400BadRequest,
            ErrorToProblemDetails = _ => null
        };

        // Act
        ProblemDetails result = NexumResultProblemDetailsMapper.CreateProblemDetails("error", options);

        // Assert
        result.Status.Should().Be(400);
        result.Title.Should().Be("Request Failed");
    }

    [Fact]
    public void CreateProblemDetails_CustomFailureStatusCode_UsesConfiguredCode()
    {
        // Arrange
        var options = new NexumEndpointOptions { FailureStatusCode = StatusCodes.Status409Conflict };

        // Act
        ProblemDetails result = NexumResultProblemDetailsMapper.CreateProblemDetails("conflict", options);

        // Assert
        result.Status.Should().Be(409);
        result.Type.Should().Be("/errors/status-409");
    }
}
