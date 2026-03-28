using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.Advanced;

[Trait("Category", "E2E")]
public sealed class LifetimeE2ETests : IDisposable
{
    private readonly Microsoft.Extensions.Hosting.IHost _host;

    public LifetimeE2ETests()
    {
        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddNexumHandler<ICommandHandler<TransientTestCommand, Guid>, TransientTestHandler>(NexumLifetime.Transient);
            services.AddNexumHandler<ICommandHandler<SingletonTestCommand, Guid>, SingletonTestHandler>(NexumLifetime.Singleton);
        });
    }

    public void Dispose() => _host.Dispose();

    // E2E-050: Transient handler dispatched twice in same scope — different InstanceIds each time
    [Fact]
    public async Task DispatchAsync_TransientHandlerDispatchedTwice_ReturnsDifferentInstanceIds()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();

        var id1 = await dispatcher.DispatchAsync(new TransientTestCommand(), ct);
        var id2 = await dispatcher.DispatchAsync(new TransientTestCommand(), ct);

        id1.Should().NotBe(id2);
    }

    // E2E-051: Scoped handler dispatched twice in same scope — same InstanceId (one instance per scope)
    [Fact]
    public async Task DispatchAsync_ScopedHandlerDispatchedTwiceInSameScope_ReturnsSameInstanceId()
    {
        var ct = TestContext.Current.CancellationToken;

        using var scope = _host.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var id1 = await dispatcher.DispatchAsync(new ScopedTestCommand(), ct);
        var id2 = await dispatcher.DispatchAsync(new ScopedTestCommand(), ct);

        id1.Should().Be(id2);
    }

    // E2E-052: Singleton handler dispatched across two separate scopes — same InstanceId (one instance for lifetime)
    [Fact]
    public async Task DispatchAsync_SingletonHandlerAcrossTwoScopes_ReturnsSameInstanceId()
    {
        var ct = TestContext.Current.CancellationToken;

        Guid id1;
        Guid id2;

        using (var scope1 = _host.Services.CreateScope())
        {
            var dispatcher = scope1.ServiceProvider.GetRequiredService<ICommandDispatcher>();
            id1 = await dispatcher.DispatchAsync(new SingletonTestCommand(), ct);
        }

        using (var scope2 = _host.Services.CreateScope())
        {
            var dispatcher = scope2.ServiceProvider.GetRequiredService<ICommandDispatcher>();
            id2 = await dispatcher.DispatchAsync(new SingletonTestCommand(), ct);
        }

        id1.Should().Be(id2);
    }
}
