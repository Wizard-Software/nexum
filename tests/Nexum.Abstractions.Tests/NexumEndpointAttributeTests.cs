namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class NexumEndpointAttributeTests
{
    [Fact]
    public void Constructor_SetsMethodAndPattern()
    {
        // Arrange & Act
        var attr = new NexumEndpointAttribute(NexumHttpMethod.Post, "/api/orders");

        // Assert
        attr.Method.Should().Be(NexumHttpMethod.Post);
        attr.Pattern.Should().Be("/api/orders");
        attr.Name.Should().BeNull();
        attr.GroupName.Should().BeNull();
    }

    [Fact]
    public void Name_CanBeSet()
    {
        // Arrange & Act
        var attr = new NexumEndpointAttribute(NexumHttpMethod.Get, "/api/orders/{id}")
        {
            Name = "GetOrder"
        };

        // Assert
        attr.Name.Should().Be("GetOrder");
    }

    [Fact]
    public void GroupName_CanBeSet()
    {
        // Arrange & Act
        var attr = new NexumEndpointAttribute(NexumHttpMethod.Delete, "/api/orders/{id}")
        {
            GroupName = "Orders"
        };

        // Assert
        attr.GroupName.Should().Be("Orders");
    }

    [Fact]
    public void AttributeUsage_TargetsClassOnly_NotInherited()
    {
        // Arrange & Act
        var usage = typeof(NexumEndpointAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        usage.ValidOn.Should().Be(AttributeTargets.Class);
        usage.Inherited.Should().BeFalse();
    }
}
