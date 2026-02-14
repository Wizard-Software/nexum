using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore.Tests;

[Trait("Category", "Unit")]
public sealed class NexumEndpointOptionsTests
{
    [Fact]
    public void Defaults_SuccessStatusCode_Is200()
    {
        // Arrange & Act
        var options = new NexumEndpointOptions();

        // Assert
        options.SuccessStatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public void Defaults_FailureStatusCode_Is400()
    {
        // Arrange & Act
        var options = new NexumEndpointOptions();

        // Assert
        options.FailureStatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void Defaults_ErrorToProblemDetails_IsNull()
    {
        // Arrange & Act
        var options = new NexumEndpointOptions();

        // Assert
        options.ErrorToProblemDetails.Should().BeNull();
    }

    [Fact]
    public void ErrorToProblemDetails_CanBeSet()
    {
        // Arrange
        var options = new NexumEndpointOptions
        {
            ErrorToProblemDetails = _ => new ProblemDetails { Status = 422 }
        };

        // Assert
        options.ErrorToProblemDetails.Should().NotBeNull();
    }
}
