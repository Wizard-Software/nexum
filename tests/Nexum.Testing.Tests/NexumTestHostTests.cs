using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;

namespace Nexum.Testing.Tests;

[Trait("Category", "Integration")]
public sealed class NexumTestHostTests
{
    [Fact]
    public async Task Create_WithHandler_DispatchesSuccessfullyAsync()
    {
        // Arrange
        using var host = NexumTestHost.Create(b =>
            b.AddHandler<ICommandHandler<HostTestCommand, string>, HostTestCommandHandler>());

        // Act
        var result = await host.CommandDispatcher.DispatchAsync(
            new HostTestCommand("hello"), CancellationToken.None);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task Create_WithQueryHandler_DispatchesSuccessfullyAsync()
    {
        // Arrange
        using var host = NexumTestHost.Create(b =>
            b.AddHandler<IQueryHandler<HostTestQuery, string>, HostTestQueryHandler>());

        // Act
        var result = await host.QueryDispatcher.DispatchAsync(
            new HostTestQuery("my-filter"), CancellationToken.None);

        // Assert
        result.Should().Be("my-filter");
    }

    [Fact]
    public async Task Create_WithConfigure_AppliesOptionsAsync()
    {
        // Arrange — set MaxDispatchDepth to 1 so any re-entrant call exceeds the limit
        using var host = NexumTestHost.Create(b =>
        {
            b.Configure(opts => opts.MaxDispatchDepth = 1);
            b.AddHandler<ICommandHandler<HostTestCommand, string>, HostTestCommandHandler>();
        });

        // Act — first dispatch succeeds (depth == 0 → enters guard)
        var result = await host.CommandDispatcher.DispatchAsync(
            new HostTestCommand("ok"), CancellationToken.None);

        // Assert — options were applied (dispatch succeeded, meaning it used the configured depth)
        result.Should().Be("ok");

        // Verify the option is actually set
        var options = host.Services.GetRequiredService<NexumOptions>();
        options.MaxDispatchDepth.Should().Be(1);
    }

    [Fact]
    public async Task Create_WithConfigureServices_RegistersCustomServicesAsync()
    {
        // Arrange
        using var host = NexumTestHost.Create(b =>
            b.ConfigureServices(services => services.AddSingleton<HostTestCustomService>()));

        // Act
        var service = host.Services.GetService<HostTestCustomService>();

        // Assert
        service.Should().NotBeNull();
        await Task.CompletedTask; // keep async signature consistent
    }

    [Fact]
    public async Task Create_WithNotificationCollector_CollectsNotificationsAsync()
    {
        // Arrange — UseNotificationCollector is the default, but calling it explicitly here
        using var host = NexumTestHost.Create(b => b.UseNotificationCollector());

        // Act
        await host.NotificationPublisher.PublishAsync(
            new HostTestNotification("event-1"), ct: CancellationToken.None);

        // Assert
        host.NotificationCollector.Should().NotBeNull();
        host.NotificationCollector!.GetPublished<HostTestNotification>()
            .Should().ContainSingle()
            .Which.Message.Should().Be("event-1");
    }

    [Fact]
    public async Task Create_MultipleHandlerTypes_AllResolveCorrectlyAsync()
    {
        // Arrange
        using var host = NexumTestHost.Create(b =>
        {
            b.AddHandler<ICommandHandler<HostTestCommand, string>, HostTestCommandHandler>();
            b.AddHandler<IQueryHandler<HostTestQuery, string>, HostTestQueryHandler>();
        });

        // Act
        var commandResult = await host.CommandDispatcher.DispatchAsync(
            new HostTestCommand("cmd-value"), CancellationToken.None);
        var queryResult = await host.QueryDispatcher.DispatchAsync(
            new HostTestQuery("qry-filter"), CancellationToken.None);

        // Assert
        commandResult.Should().Be("cmd-value");
        queryResult.Should().Be("qry-filter");
    }

    [Fact]
    public void Dispose_DisposesServiceProvider()
    {
        // Arrange
        var host = NexumTestHost.Create(b =>
            b.AddHandler<ICommandHandler<HostTestCommand, string>, HostTestCommandHandler>());

        // Act
        host.Dispose();

        // Assert — resolving from a disposed provider throws ObjectDisposedException
        var act = () => host.Services.GetRequiredService<NexumOptions>();
        act.Should().Throw<ObjectDisposedException>();
    }
}

// ---------------------------------------------------------------------------
// Test types (prefixed with HostTest to avoid collision with other test files)
// ---------------------------------------------------------------------------

internal sealed record HostTestCommand(string Value) : ICommand<string>;
internal sealed record HostTestQuery(string Filter) : IQuery<string>;
internal sealed record HostTestNotification(string Message) : INotification;

internal sealed class HostTestCommandHandler : ICommandHandler<HostTestCommand, string>
{
    public ValueTask<string> HandleAsync(HostTestCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(command.Value);
}

internal sealed class HostTestQueryHandler : IQueryHandler<HostTestQuery, string>
{
    public ValueTask<string> HandleAsync(HostTestQuery query, CancellationToken ct = default)
        => ValueTask.FromResult(query.Filter);
}

internal sealed class HostTestCustomService
{
    public string Name => "custom";
}
