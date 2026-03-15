using AwesomeAssertions;
using Nexum.Testing;
using Xunit;
using Nexum.Examples.TestingDemo.Commands;
using Nexum.Examples.TestingDemo.Domain;
using Nexum.Examples.TestingDemo.Notifications;
using Nexum.Examples.TestingDemo.Queries;
using Nexum.Examples.TestingDemo.Services;

namespace Nexum.Examples.TestingDemo.Tests;

/// <summary>
/// Demonstrates testing a service that depends on ICommandDispatcher, IQueryDispatcher,
/// and INotificationPublisher using fakes and Nexum assertion extensions.
/// This is the key pattern: inject fake dispatchers directly — no DI container needed.
/// </summary>
public sealed class ProductServiceTests
{
    private readonly FakeCommandDispatcher _commandDispatcher = new();
    private readonly FakeQueryDispatcher _queryDispatcher = new();
    private readonly InMemoryNotificationCollector _notificationCollector = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _sut = new ProductService(_commandDispatcher, _queryDispatcher, _notificationCollector);
    }

    [Fact]
    public async Task CreateProduct_DispatchesCommandAndPublishesNotificationAsync()
    {
        var expectedId = Guid.NewGuid();
        _commandDispatcher.Setup<CreateProductCommand, Guid>().Returns(expectedId);

        var id = await _sut.CreateProductAsync("Widget", 9.99m, CancellationToken.None);

        id.Should().Be(expectedId);

        // Verify both the command was dispatched and the notification was published
        _commandDispatcher.ShouldHaveDispatched<CreateProductCommand>();
        _notificationCollector.ShouldHavePublished<ProductCreatedNotification>();
    }

    [Fact]
    public async Task GetProduct_DispatchesQuery_ReturnsResultAsync()
    {
        var id = Guid.NewGuid();
        var expectedProduct = new Product(id, "Gadget", 19.99m);
        _queryDispatcher.Setup<GetProductQuery, Product?>().Returns(expectedProduct);

        var result = await _sut.GetProductAsync(id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Gadget");

        // Verify the query was dispatched
        _queryDispatcher.ShouldHaveDispatched<GetProductQuery>();
    }

    [Fact]
    public async Task CreateProduct_ShouldHaveDispatched_WithCorrectDataAsync()
    {
        var expectedId = Guid.NewGuid();
        _commandDispatcher.Setup<CreateProductCommand, Guid>().Returns(expectedId);

        await _sut.CreateProductAsync("Premium Widget", 99.99m, CancellationToken.None);

        // ShouldHaveDispatched<T>(predicate) verifies the command was dispatched with specific data
        _commandDispatcher.ShouldHaveDispatched<CreateProductCommand>(
            cmd => cmd.Name == "Premium Widget" && cmd.Price == 99.99m);
    }

    [Fact]
    public async Task CreateProduct_ShouldHavePublished_NotificationAsync()
    {
        var expectedId = Guid.NewGuid();
        _commandDispatcher.Setup<CreateProductCommand, Guid>().Returns(expectedId);

        await _sut.CreateProductAsync("Super Gadget", 149.99m, CancellationToken.None);

        // ShouldHavePublished<T>(predicate) verifies the notification was published with correct data
        _notificationCollector.ShouldHavePublished<ProductCreatedNotification>(
            n => n.Id == expectedId && n.Name == "Super Gadget");

        // GetPublished<T>() allows direct inspection of the captured notification
        var notifications = _notificationCollector.GetPublished<ProductCreatedNotification>();
        notifications.Should().HaveCount(1);
        notifications[0].Name.Should().Be("Super Gadget");
    }
}
