namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class NexumDispatchDepthExceededExceptionTests
{
    [Fact]
    public void Constructor_SetsMaxDepth()
    {
        var exception = new NexumDispatchDepthExceededException(16);

        exception.MaxDepth.Should().Be(16);
    }

    [Fact]
    public void Constructor_SetsInformativeMessage()
    {
        var exception = new NexumDispatchDepthExceededException(16);

        exception.Message.Should().Contain("16");
        exception.Message.Should().Contain("infinite recursion");
        exception.Message.Should().Contain("NexumOptions");
        exception.Message.Should().Contain("MaxDispatchDepth");
    }

    [Fact]
    public void IsInvalidOperationException()
    {
        var exception = new NexumDispatchDepthExceededException(16);

        exception.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void IsSealed()
    {
        typeof(NexumDispatchDepthExceededException).IsSealed.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(100)]
    public void Constructor_WithVariousValues_SetsMaxDepthCorrectly(int maxDepth)
    {
        var exception = new NexumDispatchDepthExceededException(maxDepth);

        exception.MaxDepth.Should().Be(maxDepth);
    }
}
