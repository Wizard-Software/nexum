namespace Nexum.Results.Tests;

[Trait("Category", "Unit")]
public sealed class ResultOfTValueTests
{
    [Fact]
    public void Ok_WithValue_IsSuccessTrue()
    {
        // Arrange
        const int Value = 42;

        // Act
        var result = Result<int>.Ok(Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Fail_WithNexumError_IsFailureTrue()
    {
        // Arrange
        var error = new NexumError("TEST", "Test error");

        // Act
        var result = Result<int>.Fail(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Value_OnSuccess_ReturnsValue()
    {
        // Arrange
        const int Expected = 42;
        var result = Result<int>.Ok(Expected);

        // Act
        var actual = result.Value;

        // Assert
        actual.Should().Be(Expected);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        var error = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(error);

        // Act & Assert
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnFailure_ReturnsNexumError()
    {
        // Arrange
        var expectedError = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(expectedError);

        // Act
        var actualError = result.Error;

        // Assert
        actualError.Should().Be(expectedError);
    }

    [Fact]
    public void Error_OnSuccess_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act & Assert
        var act = () => result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Default_Error_ThrowsInvalidOperationException()
    {
        // Arrange
        var result = default(Result<int>);

        // Act & Assert
        var act = () => result.Error;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*invalid default state*");
    }

    [Fact]
    public void ImplicitConversion_FromInnerToResult_PreservesState()
    {
        // Arrange
        const int Value = 42;
        var inner = Result<int, NexumError>.Ok(Value);

        // Act
        Result<int> result = inner;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Value);
    }

    [Fact]
    public void ImplicitConversion_FromResultToInner_PreservesState()
    {
        // Arrange
        const int Value = 42;
        var result = Result<int>.Ok(Value);

        // Act
        Result<int, NexumError> inner = result;

        // Assert
        inner.IsSuccess.Should().BeTrue();
        inner.Value.Should().Be(Value);
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(84);
    }

    [Fact]
    public void Map_OnFailure_PropagatesNexumError()
    {
        // Arrange
        var error = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(error);

        // Act
        var mapped = result.Map(x => x * 2);

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Bind_OnSuccess_ChainsResult()
    {
        // Arrange
        var result = Result<int>.Ok(42);

        // Act
        var bound = result.Bind(x => Result<string>.Ok(x.ToString()));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("42");
    }

    [Fact]
    public void Bind_OnFailure_PropagatesNexumError()
    {
        // Arrange
        var error = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(error);

        // Act
        var bound = result.Bind(x => Result<string>.Ok(x.ToString()));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(error);
    }

    [Fact]
    public void GetValueOrDefault_OnSuccess_ReturnsValue()
    {
        // Arrange
        const int Value = 42;
        var result = Result<int>.Ok(Value);

        // Act
        var actual = result.GetValueOrDefault(99);

        // Assert
        actual.Should().Be(Value);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ReturnsFallback()
    {
        // Arrange
        var error = new NexumError("TEST", "Test error");
        var result = Result<int>.Fail(error);
        const int Fallback = 99;

        // Act
        var actual = result.GetValueOrDefault(Fallback);

        // Assert
        actual.Should().Be(Fallback);
    }
}
