#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Core;

[Trait("Category", "E2E")]
public sealed class CancellationTokenE2ETests : IDisposable
{
    private readonly IHost _host = NexumTestHost.CreateHost();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task DispatchAsync_WithCancelledToken_ThrowsOperationCanceledExceptionAsync()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        var act = async () => await dispatcher.DispatchAsync(new SlowCommand(5000), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DispatchAsync_CancelledMidExecution_ThrowsOperationCanceledExceptionAsync()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert — SlowCommand delays 2000ms, cancellation fires after 50ms
        var act = async () => await dispatcher.DispatchAsync(new SlowCommand(2000), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StreamAsync_CancelledMidIteration_StopsYieldingAsync()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        using var cts = new CancellationTokenSource();
        var received = new List<ItemDto>();

        // Act — cancel after receiving 3 items
        var act = async () =>
        {
            await foreach (var item in dispatcher.StreamAsync(new ListItemsStreamQuery(100), cts.Token))
            {
                received.Add(item);
                if (received.Count >= 3)
                {
                    await cts.CancelAsync();
                }
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        received.Should().HaveCount(3);
    }
}
