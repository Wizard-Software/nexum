namespace Nexum.Batching.Tests;

[Trait("Category", "Unit")]
public sealed class NexumBatchingOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new NexumBatchingOptions();

        // Assert
        options.BatchWindow.Should().Be(TimeSpan.FromMilliseconds(10));
        options.MaxBatchSize.Should().Be(100);
        options.DrainTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void BatchWindow_BelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new NexumBatchingOptions();

        // Act
        Action act = () => options.BatchWindow = TimeSpan.FromMilliseconds(0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void BatchWindow_AboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new NexumBatchingOptions();

        // Act
        Action act = () => options.BatchWindow = TimeSpan.FromSeconds(31);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void MaxBatchSize_Zero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new NexumBatchingOptions();

        // Act
        Action act = () => options.MaxBatchSize = 0;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void MaxBatchSize_AboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new NexumBatchingOptions();

        // Act
        Action act = () => options.MaxBatchSize = 10_001;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void DrainTimeout_Zero_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new NexumBatchingOptions();

        // Act
        Action act = () => options.DrainTimeout = TimeSpan.Zero;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void BatchWindow_ValidValue_SetsSuccessfully()
    {
        // Arrange
        var options = new NexumBatchingOptions();
        var expectedValue = TimeSpan.FromMilliseconds(100);

        // Act
        options.BatchWindow = expectedValue;

        // Assert
        options.BatchWindow.Should().Be(expectedValue);
    }

    [Fact]
    public void MaxBatchSize_ValidValue_SetsSuccessfully()
    {
        // Arrange
        var options = new NexumBatchingOptions();
        const int ExpectedValue = 500;

        // Act
        options.MaxBatchSize = ExpectedValue;

        // Assert
        options.MaxBatchSize.Should().Be(ExpectedValue);
    }

    [Fact]
    public void DrainTimeout_ValidValue_SetsSuccessfully()
    {
        // Arrange
        var options = new NexumBatchingOptions();
        var expectedValue = TimeSpan.FromSeconds(10);

        // Act
        options.DrainTimeout = expectedValue;

        // Assert
        options.DrainTimeout.Should().Be(expectedValue);
    }
}
