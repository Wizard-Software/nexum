using Nexum.Abstractions;
using Nexum.Internal;

namespace Nexum.Tests;

/// <summary>
/// Tests for <see cref="NotificationEnvelope"/> readonly record struct.
/// Verifies value semantics, boxing behavior, and structural properties.
/// </summary>
[Trait("Category", "Unit")]
public sealed class NotificationEnvelopeTests
{
    [Fact]
    public void Constructor_WithValidArgs_StoresAllProperties()
    {
        // Arrange
        var notification = new TestNotification("test");
        var notificationType = notification.GetType();
        var context = ExecutionContext.Capture();

        // Act
        var envelope = new NotificationEnvelope(notification, notificationType, context);

        // Assert
        envelope.Notification.Should().BeSameAs(notification);
        envelope.NotificationType.Should().Be(notificationType);
        envelope.CapturedContext.Should().BeSameAs(context);
    }

    [Fact]
    public void ValueEquality_SameValues_AreEqual()
    {
        // Arrange
        var notification = new TestNotification("test");
        var notificationType = notification.GetType();

        var envelope1 = new NotificationEnvelope(notification, notificationType, null);
        var envelope2 = new NotificationEnvelope(notification, notificationType, null);

        // Act & Assert
        envelope1.Should().Be(envelope2);
        (envelope1 == envelope2).Should().BeTrue();
    }

    [Fact]
    public void ValueEquality_DifferentValues_AreNotEqual()
    {
        // Arrange
        var notification1 = new TestNotification("test1");
        var notification2 = new TestNotification("test2");
        var notificationType = typeof(TestNotification);

        var envelope1 = new NotificationEnvelope(notification1, notificationType, null);
        var envelope2 = new NotificationEnvelope(notification2, notificationType, null);

        // Act & Assert
        envelope1.Should().NotBe(envelope2);
        (envelope1 != envelope2).Should().BeTrue();
    }

    [Fact]
    public void CapturedContext_CanBeNull()
    {
        // Arrange & Act
        var envelope = new NotificationEnvelope(
            new TestNotification("test"),
            typeof(TestNotification),
            null);

        // Assert
        envelope.CapturedContext.Should().BeNull();
    }

    [Fact]
    public void NotificationType_StoresRuntimeType()
    {
        // Arrange — notification stored as INotification (boxed), but runtime type preserved separately
        INotification notification = new TestNotification("test");
        var runtimeType = notification.GetType(); // typeof(TestNotification)

        // Act
        var envelope = new NotificationEnvelope(notification, runtimeType, null);

        // Assert — NotificationType is the concrete type, not the interface
        envelope.NotificationType.Should().Be(typeof(TestNotification));
        envelope.NotificationType.Should().NotBe(typeof(INotification));
    }

    private sealed record TestNotification(string Value) : INotification;
}
