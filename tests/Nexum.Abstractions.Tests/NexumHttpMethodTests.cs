namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class NexumHttpMethodTests
{
    [Theory]
    [InlineData(NexumHttpMethod.Get, 0)]
    [InlineData(NexumHttpMethod.Post, 1)]
    [InlineData(NexumHttpMethod.Put, 2)]
    [InlineData(NexumHttpMethod.Delete, 3)]
    [InlineData(NexumHttpMethod.Patch, 4)]
    public void EnumValues_HaveExpectedUnderlyingValues(NexumHttpMethod method, int expected)
    {
        ((int)method).Should().Be(expected);
    }

    [Fact]
    public void EnumValues_ContainAllExpectedMethods()
    {
        var values = Enum.GetValues<NexumHttpMethod>();
        values.Should().HaveCount(5);
        values.Should().Contain([
            NexumHttpMethod.Get,
            NexumHttpMethod.Post,
            NexumHttpMethod.Put,
            NexumHttpMethod.Delete,
            NexumHttpMethod.Patch
        ]);
    }
}
