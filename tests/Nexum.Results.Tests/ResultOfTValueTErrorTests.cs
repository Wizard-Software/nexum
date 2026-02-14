namespace Nexum.Results.Tests;

[Trait("Category", "Unit")]
public sealed class ResultOfTValueTErrorTests
{
    [Fact]
    public void Ok_WithValue_IsSuccessTrue()
    {
        // Arrange
        const int Value = 42;

        // Act
        Result<int, string> result = Result<int, string>.Ok(Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Fail_WithError_IsFailureTrue()
    {
        // Arrange
        const string Error = "Something went wrong";

        // Act
        Result<int, string> result = Result<int, string>.Fail(Error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Value_OnSuccess_ReturnsValue()
    {
        // Arrange
        const int Expected = 42;
        Result<int, string> result = Result<int, string>.Ok(Expected);

        // Act
        int actual = result.Value;

        // Assert
        actual.Should().Be(Expected);
    }

    [Fact]
    public void Value_OnFailure_ThrowsInvalidOperationException()
    {
        // Arrange
        Result<int, string> result = Result<int, string>.Fail("Error");

        // Act & Assert
        Action act = () => _ = result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Error_OnFailure_ReturnsError()
    {
        // Arrange
        const string Expected = "Something went wrong";
        Result<int, string> result = Result<int, string>.Fail(Expected);

        // Act
        string actual = result.Error;

        // Assert
        actual.Should().Be(Expected);
    }

    [Fact]
    public void Error_OnSuccess_ThrowsInvalidOperationException()
    {
        // Arrange
        Result<int, string> result = Result<int, string>.Ok(42);

        // Act & Assert
        Action act = () => _ = result.Error;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_OnSuccess_TransformsValue()
    {
        // Arrange
        const int Value = 42;
        Result<int, string> result = Result<int, string>.Ok(Value);

        // Act
        Result<string, string> mapped = result.Map(x => x.ToString());

        // Assert
        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be("42");
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        // Arrange
        const string Error = "Error";
        Result<int, string> result = Result<int, string>.Fail(Error);
        bool mapperCalled = false;

        // Act
        Result<string, string> mapped = result.Map(x =>
        {
            mapperCalled = true;
            return x.ToString();
        });

        // Assert
        mapped.IsFailure.Should().BeTrue();
        mapped.Error.Should().Be(Error);
        mapperCalled.Should().BeFalse();
    }

    [Fact]
    public void Bind_OnSuccess_ChainsResult()
    {
        // Arrange
        const int Value = 21;
        Result<int, string> result = Result<int, string>.Ok(Value);

        // Act
        Result<int, string> bound = result.Bind(x => Result<int, string>.Ok(x * 2));

        // Assert
        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be(42);
    }

    [Fact]
    public void Bind_OnFailure_PropagatesError()
    {
        // Arrange
        const string Error = "Error";
        Result<int, string> result = Result<int, string>.Fail(Error);

        // Act
        Result<int, string> bound = result.Bind(x => Result<int, string>.Ok(x * 2));

        // Assert
        bound.IsFailure.Should().BeTrue();
        bound.Error.Should().Be(Error);
    }

    [Fact]
    public void GetValueOrDefault_OnSuccess_ReturnsValue()
    {
        // Arrange
        const int Value = 42;
        const int Fallback = 99;
        Result<int, string> result = Result<int, string>.Ok(Value);

        // Act
        int actual = result.GetValueOrDefault(Fallback);

        // Assert
        actual.Should().Be(Value);
    }

    [Fact]
    public void GetValueOrDefault_OnFailure_ReturnsFallback()
    {
        // Arrange
        const int Fallback = 99;
        Result<int, string> result = Result<int, string>.Fail("Error");

        // Act
        int actual = result.GetValueOrDefault(Fallback);

        // Assert
        actual.Should().Be(Fallback);
    }

    [Fact]
    public void Default_IsFailure()
    {
        // Arrange & Act
        Result<int, string> result = default;

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Default_Error_ReturnsDefaultTError()
    {
        // Arrange
        Result<int, string> result = default;

        // Act
        string error = result.Error;

        // Assert
        error.Should().Be(default);
    }
}
