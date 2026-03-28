using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Core;

[Trait("Category", "E2E")]
public sealed class StreamQueryE2ETests : IDisposable
{
    private readonly IHost _host = NexumTestHost.CreateHost();

    public void Dispose() => _host.Dispose();

    /// <summary>
    /// E2E-004: ListItemsStreamQuery yields exactly the requested number of items.
    /// </summary>
    [Fact]
    public async Task StreamAsync_ListItemsStreamQuery_YieldsExactlyCountItems()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        const int expectedCount = 5;
        var query = new ListItemsStreamQuery(Count: expectedCount);
        var items = new List<ItemDto>();

        // Act
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        // Assert
        items.Should().HaveCount(expectedCount);
        items.Should().OnlyContain(item => item.Id != Guid.Empty);
        items.Should().OnlyContain(item => item.Name.StartsWith("Item-"));
    }

    /// <summary>
    /// E2E-101: Cancelling the token mid-stream after 3 items throws OperationCanceledException.
    /// </summary>
    [Fact]
    public async Task StreamAsync_CancelledMidIteration_ThrowsOperationCanceledException()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        var query = new ListItemsStreamQuery(Count: 20);
        var receivedCount = 0;

        // Act
        var act = async () =>
        {
            await foreach (var _ in dispatcher.StreamAsync(query, cts.Token))
            {
                receivedCount++;
                if (receivedCount >= 3)
                {
                    await cts.CancelAsync();
                }
            }
        };

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        receivedCount.Should().Be(3);
    }

    /// <summary>
    /// E2E-033: StreamAsync with an unregistered stream query type throws NexumHandlerNotFoundException
    /// immediately from the StreamAsync() call itself — not deferred to the first iteration.
    /// </summary>
    [Fact]
    public void StreamAsync_UnregisteredStreamQuery_ThrowsImmediatelyBeforeIteration()
    {
        // Arrange
        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Inline unregistered stream query — no handler registered in the test assembly
        var query = new UnregisteredStreamQuery();

        // Act — synchronous lambda: StreamAsync is NOT async, exception fires at call site
        var act = () => dispatcher.StreamAsync(query, TestContext.Current.CancellationToken);

        // Assert — sync throw (no await), proving the exception is not deferred to enumeration
        act.Should().Throw<NexumHandlerNotFoundException>()
            .WithMessage($"*{nameof(UnregisteredStreamQuery)}*");
    }
}

// Inline record — no handler registered anywhere in the test assembly
file sealed record UnregisteredStreamQuery : IStreamQuery<string>;
