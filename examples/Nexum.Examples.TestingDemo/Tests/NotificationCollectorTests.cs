using AwesomeAssertions;
using Nexum.Testing;
using Xunit;
using Nexum.Examples.TestingDemo.Notifications;

namespace Nexum.Examples.TestingDemo.Tests;

/// <summary>
/// Demonstrates InMemoryNotificationCollector for verifying notification publishing.
/// The collector implements INotificationPublisher and captures all published notifications.
/// </summary>
public sealed class NotificationCollectorTests
{
    [Fact]
    public async Task InMemoryCollector_PublishAsync_CollectsNotificationAsync()
    {
        var collector = new InMemoryNotificationCollector();
        var notification = new ProductCreatedNotification(Guid.NewGuid(), "Widget");

        // PublishAsync captures the notification instead of dispatching to real handlers
        await collector.PublishAsync(notification, ct: CancellationToken.None);

        collector.PublishedNotifications.Should().HaveCount(1);
        collector.PublishedNotifications[0].Should().Be(notification);
    }

    [Fact]
    public async Task InMemoryCollector_GetPublished_FiltersCorrectTypeAsync()
    {
        var collector = new InMemoryNotificationCollector();

        var productNotification = new ProductCreatedNotification(Guid.NewGuid(), "Sprocket");
        var anotherNotification = new ProductCreatedNotification(Guid.NewGuid(), "Cog");

        await collector.PublishAsync(productNotification, ct: CancellationToken.None);
        await collector.PublishAsync(anotherNotification, ct: CancellationToken.None);

        // GetPublished<T>() returns only notifications of the specified type
        var published = collector.GetPublished<ProductCreatedNotification>();
        published.Should().HaveCount(2);
        published.Should().Contain(n => n.Name == "Sprocket");
        published.Should().Contain(n => n.Name == "Cog");
    }

    [Fact]
    public async Task InMemoryCollector_Reset_ClearsAllAsync()
    {
        var collector = new InMemoryNotificationCollector();
        await collector.PublishAsync(
            new ProductCreatedNotification(Guid.NewGuid(), "Old"),
            ct: CancellationToken.None);

        collector.PublishedNotifications.Should().HaveCount(1);

        // Reset() clears all captured notifications — useful between test scenarios
        collector.Reset();

        collector.PublishedNotifications.Should().BeEmpty();
    }
}
