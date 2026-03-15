using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Testing;
using Xunit;
using Nexum.Examples.TestingDemo.Commands;
using Nexum.Examples.TestingDemo.Domain;
using Nexum.Examples.TestingDemo.Notifications;
using Nexum.Examples.TestingDemo.Queries;

namespace Nexum.Examples.TestingDemo.Tests;

/// <summary>
/// Demonstrates using NexumTestHost for integration-style tests with real handlers.
/// NexumTestHost wires up the full Nexum dispatch pipeline in-memory — no server required.
/// </summary>
public sealed class NexumTestHostTests
{
    [Fact]
    public async Task CreateHost_DispatchCommand_ReturnsResultAsync()
    {
        // NexumTestHost.Create() registers the full Nexum pipeline.
        // AddHandler<TService, TImpl>() registers the handler interface → implementation mapping.
        // ConfigureServices() gives access to the IServiceCollection for custom registrations.
        using var host = NexumTestHost.Create(b => b
            .AddHandler<ICommandHandler<CreateProductCommand, Guid>, CreateProductHandler>()
            .ConfigureServices(s => s.AddSingleton(new ConcurrentDictionary<Guid, Product>())));

        var id = await host.CommandDispatcher.DispatchAsync(
            new CreateProductCommand("Widget", 9.99m),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateHost_QueryDispatch_ReturnsDataAsync()
    {
        var store = new ConcurrentDictionary<Guid, Product>();
        var existingId = Guid.NewGuid();
        store[existingId] = new Product(existingId, "Gadget", 19.99m);

        using var host = NexumTestHost.Create(b => b
            .AddHandler<IQueryHandler<GetProductQuery, Product?>, GetProductHandler>()
            .ConfigureServices(s => s.AddSingleton(store)));

        var product = await host.QueryDispatcher.DispatchAsync(
            new GetProductQuery(existingId),
            CancellationToken.None);

        product.Should().NotBeNull();
        product!.Id.Should().Be(existingId);
        product.Name.Should().Be("Gadget");
        product.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task CreateHost_NotificationPublished_CollectedByCollectorAsync()
    {
        // UseNotificationCollector() is on by default — calling it explicitly here for clarity.
        // The collector captures notifications without dispatching to real handlers.
        using var host = NexumTestHost.Create(b => b.UseNotificationCollector());

        var notification = new ProductCreatedNotification(Guid.NewGuid(), "Sprocket");
        await host.NotificationPublisher.PublishAsync(notification, ct: CancellationToken.None);

        // NotificationCollector gives typed access to captured notifications
        host.NotificationCollector.Should().NotBeNull();
        host.NotificationCollector!.ShouldHavePublished<ProductCreatedNotification>();

        var published = host.NotificationCollector.GetPublished<ProductCreatedNotification>();
        published.Should().HaveCount(1);
        published[0].Name.Should().Be("Sprocket");
    }

    [Fact]
    public void CreateHost_WithConfigure_SetsOptions()
    {
        // Configure() allows customizing NexumOptions before the host is built.
        // Here we lower MaxDispatchDepth to demonstrate configuration takes effect.
        using var host = NexumTestHost.Create(b => b
            .Configure(o => o.MaxDispatchDepth = 5));

        // Resolve the options from the DI container to verify the value was applied
        var options = host.Services.GetRequiredService<NexumOptions>();
        options.MaxDispatchDepth.Should().Be(5);
    }
}
