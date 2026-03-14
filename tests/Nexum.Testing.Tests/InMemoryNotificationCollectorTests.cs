using Nexum.Abstractions;

namespace Nexum.Testing.Tests;

[Trait("Category", "Unit")]
public sealed class InMemoryNotificationCollectorTests
{
    [Fact]
    public async Task PublishAsync_CollectsNotificationAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        var notification = new TestNotification("hello");

        // Act
        await collector.PublishAsync(notification, ct: CancellationToken.None);

        // Assert
        collector.PublishedNotifications.Should().ContainSingle()
            .Which.Should().Be(notification);
    }

    [Fact]
    public async Task GetPublished_ReturnsOnlyMatchingTypeAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        var testNotification = new TestNotification("test");
        var otherNotification = new OtherNotification(42);

        // Act
        await collector.PublishAsync(testNotification, ct: CancellationToken.None);
        await collector.PublishAsync(otherNotification, ct: CancellationToken.None);

        // Assert
        var testResults = collector.GetPublished<TestNotification>();
        testResults.Should().ContainSingle().Which.Should().Be(testNotification);

        var otherResults = collector.GetPublished<OtherNotification>();
        otherResults.Should().ContainSingle().Which.Should().Be(otherNotification);
    }

    [Fact]
    public async Task Reset_ClearsAllNotificationsAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        await collector.PublishAsync(new TestNotification("one"), ct: CancellationToken.None);
        await collector.PublishAsync(new TestNotification("two"), ct: CancellationToken.None);

        // Act
        collector.Reset();

        // Assert
        collector.PublishedNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_MultipleNotifications_CollectsAllAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        var n1 = new TestNotification("first");
        var n2 = new TestNotification("second");
        var n3 = new TestNotification("third");

        // Act
        await collector.PublishAsync(n1, ct: CancellationToken.None);
        await collector.PublishAsync(n2, ct: CancellationToken.None);
        await collector.PublishAsync(n3, ct: CancellationToken.None);

        // Assert
        collector.PublishedNotifications.Should().HaveCount(3);
        collector.PublishedNotifications[0].Should().Be(n1);
        collector.PublishedNotifications[1].Should().Be(n2);
        collector.PublishedNotifications[2].Should().Be(n3);
    }

    [Fact]
    public async Task PublishAsync_ThreadSafe_CollectsAllConcurrentAsync()
    {
        // Arrange
        const int ThreadCount = 20;
        const int NotificationsPerThread = 50;
        var collector = new InMemoryNotificationCollector();

        // Act — publish from multiple threads concurrently
        var tasks = Enumerable.Range(0, ThreadCount)
            .Select(i => Task.Run(async () =>
            {
                for (var j = 0; j < NotificationsPerThread; j++)
                {
                    await collector.PublishAsync(
                        new TestNotification($"t{i}-n{j}"),
                        ct: CancellationToken.None);
                }
            }));

        await Task.WhenAll(tasks);

        // Assert — all notifications collected
        collector.PublishedNotifications.Should().HaveCount(ThreadCount * NotificationsPerThread);
    }
}

internal sealed record TestNotification(string Message) : INotification;
internal sealed record OtherNotification(int Value) : INotification;
