#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode warning for test usage

using Nexum.Results.FluentValidation.Internal;

namespace Nexum.Results.FluentValidation.Tests;

[Trait("Category", "Unit")]
public class DefaultResultFailureFactoryTests
{
    [Fact]
    public void CanCreate_ResultT_ReturnsTrue()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();

        // Act
        var result = factory.CanCreate(typeof(Result<Guid>));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanCreate_ResultTNexumError_ReturnsTrue()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();

        // Act
        var result = factory.CanCreate(typeof(Result<Guid, NexumError>));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanCreate_NonResultType_ReturnsFalse()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();

        // Act
        var result = factory.CanCreate(typeof(Guid));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanCreate_ResultTWithDifferentError_ReturnsFalse()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();

        // Act
        var result = factory.CanCreate(typeof(Result<Guid, string>));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanCreate_ReferenceType_ReturnsFalse()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();

        // Act
        var result = factory.CanCreate(typeof(string));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateFailure_ResultT_ReturnsFailResult()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();
        var error = new NexumError("TEST", "Test error");

        // Act
        var result = (Result<Guid>)factory.CreateFailure(typeof(Result<Guid>), error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void CreateFailure_ResultTNexumError_ReturnsFailResult()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();
        var error = new NexumError("TEST", "Test error");

        // Act
        var result = (Result<Guid, NexumError>)factory.CreateFailure(typeof(Result<Guid, NexumError>), error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void CreateFailure_UnsupportedType_ThrowsInvalidOperationException()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();
        var error = new NexumError("TEST", "Test error");

        // Act
        Action act = () => factory.CreateFailure(typeof(Guid), error);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot create failure Result for type 'System.Guid'.");
    }

    [Fact]
    public void CreateFailure_CachesFactoryDelegate()
    {
        // Arrange
        var factory = new DefaultResultFailureFactory();
        var error1 = new NexumError("TEST1", "Test error 1");
        var error2 = new NexumError("TEST2", "Test error 2");

        // Act
        var result1 = (Result<int>)factory.CreateFailure(typeof(Result<int>), error1);
        var result2 = (Result<int>)factory.CreateFailure(typeof(Result<int>), error2);

        // Assert
        result1.Error.Should().Be(error1);
        result2.Error.Should().Be(error2);
    }
}
