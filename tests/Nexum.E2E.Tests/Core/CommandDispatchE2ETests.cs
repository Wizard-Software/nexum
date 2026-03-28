using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Core;

[Trait("Category", "E2E")]
public sealed class CommandDispatchE2ETests : IDisposable
{
    private readonly IHost _host = NexumTestHost.CreateHost();

    public void Dispose() => _host.Dispose();

    /// <summary>
    /// E2E-001: CreateItemCommand dispatch returns a non-empty Guid and the item is stored in the shared ConcurrentDictionary.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_CreateItemCommand_ReturnsGuidAndStoresItem()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var store = _host.Services.GetRequiredService<ConcurrentDictionary<Guid, ItemDto>>();
        var command = new CreateItemCommand("Test Item");

        // Act
        var id = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        id.Should().NotBeEmpty();
        store.Should().ContainKey(id);
        store[id].Name.Should().Be("Test Item");
    }

    /// <summary>
    /// E2E-002: DeleteItemCommand (IVoidCommand) returns Unit and removes the item from the store.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_DeleteItemCommand_ReturnsUnitAndRemovesItem()
    {
        // Arrange
        using var createScope = _host.Services.CreateScope();
        var createDispatcher = createScope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var store = _host.Services.GetRequiredService<ConcurrentDictionary<Guid, ItemDto>>();

        var createdId = await createDispatcher.DispatchAsync(
            new CreateItemCommand("To Delete"),
            TestContext.Current.CancellationToken);

        store.Should().ContainKey(createdId);

        using var deleteScope = _host.Services.CreateScope();
        var deleteDispatcher = deleteScope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var deleteCommand = new DeleteItemCommand(createdId);

        // Act
        var result = await deleteDispatcher.DispatchAsync(deleteCommand, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(Unit.Value);
        store.Should().NotContainKey(createdId);
    }

    /// <summary>
    /// E2E-005: Dispatching an UnregisteredCommand throws NexumHandlerNotFoundException with the type name in the message.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_UnregisteredCommand_ThrowsNexumHandlerNotFoundException()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var command = new UnregisteredCommand();

        // Act
        var act = async () => await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NexumHandlerNotFoundException>()
            .WithMessage($"*{nameof(UnregisteredCommand)}*");
    }
}
