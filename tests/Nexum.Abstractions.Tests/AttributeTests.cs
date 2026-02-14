namespace Nexum.Abstractions.Tests;

public sealed class AttributeTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void CommandHandlerAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(CommandHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CommandHandlerAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(CommandHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CommandHandlerAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(CommandHandlerAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void QueryHandlerAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(QueryHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void QueryHandlerAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(QueryHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void QueryHandlerAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(QueryHandlerAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StreamQueryHandlerAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(StreamQueryHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StreamQueryHandlerAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(StreamQueryHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StreamQueryHandlerAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(StreamQueryHandlerAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NotificationHandlerAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(NotificationHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NotificationHandlerAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(NotificationHandlerAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NotificationHandlerAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(NotificationHandlerAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BehaviorOrderAttribute_Constructor_SetsOrderProperty()
    {
        // Arrange & Act
        var attribute = new BehaviorOrderAttribute(42);

        // Assert
        attribute.Order.Should().Be(42);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BehaviorOrderAttribute_Constructor_AcceptsNegativeValues()
    {
        // Arrange & Act
        var attribute = new BehaviorOrderAttribute(-1);

        // Assert
        attribute.Order.Should().Be(-1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BehaviorOrderAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(BehaviorOrderAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BehaviorOrderAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(BehaviorOrderAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BehaviorOrderAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(BehaviorOrderAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_Constructor_SetsLifetimeProperty_Transient()
    {
        // Arrange & Act
        var attribute = new HandlerLifetimeAttribute(NexumLifetime.Transient);

        // Assert
        attribute.Lifetime.Should().Be(NexumLifetime.Transient);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_Constructor_SetsLifetimeProperty_Scoped()
    {
        // Arrange & Act
        var attribute = new HandlerLifetimeAttribute(NexumLifetime.Scoped);

        // Assert
        attribute.Lifetime.Should().Be(NexumLifetime.Scoped);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_Constructor_SetsLifetimeProperty_Singleton()
    {
        // Arrange & Act
        var attribute = new HandlerLifetimeAttribute(NexumLifetime.Singleton);

        // Assert
        attribute.Lifetime.Should().Be(NexumLifetime.Singleton);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_AttributeUsage_TargetsClass()
    {
        // Arrange
        var attributeType = typeof(HandlerLifetimeAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_AttributeUsage_InheritedIsFalse()
    {
        // Arrange
        var attributeType = typeof(HandlerLifetimeAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        // Assert
        attributeUsage.Inherited.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HandlerLifetimeAttribute_Class_IsSealed()
    {
        // Arrange
        var attributeType = typeof(HandlerLifetimeAttribute);

        // Act & Assert
        attributeType.IsSealed.Should().BeTrue();
    }
}
