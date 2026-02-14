namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class IResultAdapterTests
{
    // Test helpers
    private record TestResult(bool Success, string? Value, string? Error);

    private class TestResultAdapter : IResultAdapter<TestResult>
    {
        public bool IsSuccess(TestResult result) => result.Success;
        public object? GetValue(TestResult result) => result.Success ? result.Value : null;
        public object? GetError(TestResult result) => result.Success ? null : result.Error;
    }

    [Fact]
    public void MockImplementation_SatisfiesInterface()
    {
        // Arrange & Act
        var adapter = new TestResultAdapter();

        // Assert
        adapter.Should().BeAssignableTo<IResultAdapter<TestResult>>();
    }

    [Fact]
    public void IsSuccess_WithSuccessResult_ReturnsTrue()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(true, "value", null);

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsSuccess_WithFailureResult_ReturnsFalse()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(false, null, "error");

        // Act
        var isSuccess = adapter.IsSuccess(result);

        // Assert
        isSuccess.Should().BeFalse();
    }

    [Fact]
    public void GetValue_WithSuccessResult_ReturnsBoxedValue()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(true, "value", null);

        // Act
        var value = adapter.GetValue(result);

        // Assert
        value.Should().Be("value");
        value.Should().BeOfType<string>();
    }

    [Fact]
    public void GetError_WithFailureResult_ReturnsBoxedError()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(false, null, "error");

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().Be("error");
        error.Should().BeOfType<string>();
    }

    [Fact]
    public void GetValue_WithFailureResult_ReturnsNull()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(false, null, "error");

        // Act
        var value = adapter.GetValue(result);

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetError_WithSuccessResult_ReturnsNull()
    {
        // Arrange
        var adapter = new TestResultAdapter();
        var result = new TestResult(true, "value", null);

        // Act
        var error = adapter.GetError(result);

        // Assert
        error.Should().BeNull();
    }
}
