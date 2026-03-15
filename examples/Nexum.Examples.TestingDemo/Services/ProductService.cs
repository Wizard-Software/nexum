using Nexum.Abstractions;
using Nexum.Examples.TestingDemo.Commands;
using Nexum.Examples.TestingDemo.Domain;
using Nexum.Examples.TestingDemo.Notifications;
using Nexum.Examples.TestingDemo.Queries;

namespace Nexum.Examples.TestingDemo.Services;

public sealed class ProductService(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    INotificationPublisher notificationPublisher)
{
    public async Task<Guid> CreateProductAsync(string name, decimal price, CancellationToken ct = default)
    {
        var id = await commandDispatcher.DispatchAsync(new CreateProductCommand(name, price), ct)
            .ConfigureAwait(false);

        await notificationPublisher.PublishAsync(new ProductCreatedNotification(id, name), ct: ct)
            .ConfigureAwait(false);

        return id;
    }

    public async Task<Product?> GetProductAsync(Guid id, CancellationToken ct = default)
    {
        return await queryDispatcher.DispatchAsync(new GetProductQuery(id), ct)
            .ConfigureAwait(false);
    }
}
