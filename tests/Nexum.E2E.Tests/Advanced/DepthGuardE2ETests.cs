using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Advanced;

[Trait("Category", "E2E")]
public sealed class DepthGuardE2ETests : IDisposable
{
    private readonly Microsoft.Extensions.Hosting.IHost _host;

    public DepthGuardE2ETests()
    {
        _host = NexumTestHost.CreateHost();
    }

    public void Dispose() => _host.Dispose();

    // E2E-040: Non-recursive dispatch with MaxDispatchDepth=3 — single depth, succeeds and returns "Leaf"
    [Fact]
    public async Task DispatchAsync_NonRecursiveCommandWithinDepthLimit_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = NexumTestHost.CreateHost(
            configureOptions: options => options.MaxDispatchDepth = 3);

        var dispatcher = host.Services.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new RecursiveCommand(ShouldRecurse: false), ct);

        result.Should().Be("Leaf");
    }

    // E2E-041: Recursive command with MaxDispatchDepth=2 — recursion exceeds limit, NexumDispatchDepthExceededException thrown
    [Fact]
    public async Task DispatchAsync_RecursiveCommandExceedsMaxDepth_ThrowsWithCorrectMaxDepth()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = NexumTestHost.CreateHost(
            configureOptions: options => options.MaxDispatchDepth = 2);

        var dispatcher = host.Services.GetRequiredService<ICommandDispatcher>();

        var act = async () => await dispatcher.DispatchAsync(new RecursiveCommand(ShouldRecurse: true), ct);

        var exception = await act.Should().ThrowAsync<NexumDispatchDepthExceededException>();
        exception.Which.MaxDepth.Should().Be(2);
    }
}
