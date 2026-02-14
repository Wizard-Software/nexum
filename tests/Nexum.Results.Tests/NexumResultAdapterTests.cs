using Nexum.Abstractions;

namespace Nexum.Results.Tests;

[Trait("Category", "Unit")]
public sealed class NexumResultAdapterTests
{
    [Fact]
    public void TwoGeneric_IsSuccess_OnSuccess_ReturnsTrue()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Ok(42);

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeTrue();
    }

    [Fact]
    public void TwoGeneric_IsSuccess_OnFailure_ReturnsFalse()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Fail("error");

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeFalse();
    }

    [Fact]
    public void TwoGeneric_GetValue_OnSuccess_ReturnsBoxedValue()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Ok(42);

        // Act
        var value = adapter.GetValue(result);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void TwoGeneric_GetValue_OnFailure_ReturnsBoxedDefault()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Fail("error");

        // Act
        var value = adapter.GetValue(result);

        // Assert — returns boxed default(int) = 0, not null (value type TValue)
        value.Should().Be(0);
    }

    [Fact]
    public void TwoGeneric_GetError_OnFailure_ReturnsBoxedError()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Fail("err");

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().Be("err");
    }

    [Fact]
    public void TwoGeneric_GetError_OnSuccess_ReturnsNull()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int, string>();
        var result = Result<int, string>.Ok(42);

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void SingleGeneric_IsSuccess_OnSuccess_ReturnsTrue()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var result = Result<int>.Ok(42);

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeTrue();
    }

    [Fact]
    public void SingleGeneric_IsSuccess_OnFailure_ReturnsFalse()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var result = Result<int>.Fail(new NexumError("TEST", "Test error"));

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeFalse();
    }

    [Fact]
    public void SingleGeneric_GetValue_OnSuccess_ReturnsBoxedValue()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var result = Result<int>.Ok(42);

        // Act
        var value = adapter.GetValue(result);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void SingleGeneric_GetValue_OnFailure_ReturnsBoxedDefault()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var result = Result<int>.Fail(new NexumError("TEST", "Test error"));

        // Act
        var value = adapter.GetValue(result);

        // Assert — returns boxed default(int) = 0, not null (value type TValue)
        value.Should().Be(0);
    }

    [Fact]
    public void SingleGeneric_GetError_OnFailure_ReturnsNexumError()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var expectedError = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(expectedError);

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().Be(expectedError);
    }

    [Fact]
    public void SingleGeneric_GetError_OnSuccess_ReturnsNull()
    {
        // Arrange
        var adapter = new NexumResultAdapter<int>();
        var result = Result<int>.Ok(42);

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().BeNull();
    }

    [Fact]
    public void ImplementsIResultAdapter_TwoGeneric()
    {
        // Arrange & Act
        var implementsInterface = typeof(IResultAdapter<Result<int, string>>)
            .IsAssignableFrom(typeof(NexumResultAdapter<int, string>));

        // Assert
        implementsInterface.Should().BeTrue();
    }

    [Fact]
    public void ImplementsIResultAdapter_SingleGeneric()
    {
        // Arrange & Act
        var implementsInterface = typeof(IResultAdapter<Result<int>>)
            .IsAssignableFrom(typeof(NexumResultAdapter<int>));

        // Assert
        implementsInterface.Should().BeTrue();
    }
}
