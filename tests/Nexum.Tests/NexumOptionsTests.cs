using Nexum.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class NexumOptionsTests
{
    [Fact]
    public void FireAndForgetTimeout_ZeroOrNegative_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var options = new NexumOptions();

        // Act & Assert
        FluentActions.Invoking(() => options.FireAndForgetTimeout = TimeSpan.Zero)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");

        FluentActions.Invoking(() => options.FireAndForgetTimeout = TimeSpan.FromSeconds(-1))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void FireAndForgetChannelCapacity_ZeroOrNegative_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var options = new NexumOptions();

        // Act & Assert
        FluentActions.Invoking(() => options.FireAndForgetChannelCapacity = 0)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");

        FluentActions.Invoking(() => options.FireAndForgetChannelCapacity = -1)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void FireAndForgetDrainTimeout_ZeroOrNegative_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var options = new NexumOptions();

        // Act & Assert
        FluentActions.Invoking(() => options.FireAndForgetDrainTimeout = TimeSpan.Zero)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");

        FluentActions.Invoking(() => options.FireAndForgetDrainTimeout = TimeSpan.FromSeconds(-1))
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void MaxDispatchDepth_ZeroOrNegative_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var options = new NexumOptions();

        // Act & Assert
        FluentActions.Invoking(() => options.MaxDispatchDepth = 0)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");

        FluentActions.Invoking(() => options.MaxDispatchDepth = -1)
            .Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("value");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new NexumOptions();

        // Assert
        options.DefaultPublishStrategy.Should().Be(PublishStrategy.Sequential);
        options.FireAndForgetTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.FireAndForgetChannelCapacity.Should().Be(1000);
        options.FireAndForgetDrainTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.MaxDispatchDepth.Should().Be(16);
    }
}
