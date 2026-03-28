using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Core;

[Trait("Category", "E2E")]
public sealed class QueryDispatchE2ETests : IDisposable
{
    private readonly IHost _host = NexumTestHost.CreateHost();

    public void Dispose() => _host.Dispose();

    /// <summary>
    /// E2E-003: GetItemQuery returns the correct ItemDto after the item is created via a command.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_GetItemQuery_ReturnsItemDtoForExistingId()
    {
        // Arrange — create an item first via command dispatch
        using var createScope = _host.Services.CreateScope();
        var commandDispatcher = createScope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var createdId = await commandDispatcher.DispatchAsync(
            new CreateItemCommand("Query Target"),
            TestContext.Current.CancellationToken);

        using var queryScope = _host.Services.CreateScope();
        var queryDispatcher = queryScope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        var query = new GetItemQuery(createdId);

        // Act
        var item = await queryDispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        item.Should().NotBeNull();
        item!.Id.Should().Be(createdId);
        item.Name.Should().Be("Query Target");
    }

    /// <summary>
    /// E2E-003b: GetItemQuery returns null when no item with the given ID exists.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_GetItemQuery_ReturnsNullForNonExistentId()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        var query = new GetItemQuery(Guid.CreateVersion7());

        // Act
        var item = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        item.Should().BeNull();
    }
}
