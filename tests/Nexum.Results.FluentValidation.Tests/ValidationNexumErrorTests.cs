using FluentValidation.Results;

namespace Nexum.Results.FluentValidation.Tests;

[Trait("Category", "Unit")]
public class ValidationNexumErrorTests
{
    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        // Arrange
        var code = "VALIDATION_FAILED";
        var message = "Name is required; Amount must be greater than 0";
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Amount", "Amount must be greater than 0")
        };

        // Act
        var error = new ValidationNexumError(code, message, failures);

        // Assert
        error.Code.Should().Be(code);
        error.Message.Should().Be(message);
        error.Failures.Should().HaveCount(2);
        error.Failures.Should().BeEquivalentTo(failures);
    }

    [Fact]
    public void ValidationNexumError_InheritsFromNexumError()
    {
        // Arrange
        var error = new ValidationNexumError("CODE", "Message", []);

        // Act & Assert
        (error is NexumError).Should().BeTrue();
    }

    [Fact]
    public void Constructor_PassesCodeAndMessageToBase()
    {
        // Arrange
        var code = "TEST_CODE";
        var message = "Test message";
        var failures = new List<ValidationFailure> { new("Field", "Error") };

        // Act
        var error = new ValidationNexumError(code, message, failures);
        NexumError baseError = error;

        // Assert
        baseError.Code.Should().Be(code);
        baseError.Message.Should().Be(message);
    }

    [Fact]
    public void Constructor_WithEmptyFailures_Works()
    {
        // Arrange
        var code = "CODE";
        var message = "Message";
        var failures = new List<ValidationFailure>();

        // Act
        var error = new ValidationNexumError(code, message, failures);

        // Assert
        error.Failures.Should().BeEmpty();
    }
}
