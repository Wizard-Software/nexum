using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Integration")]
public class AddNexumWithMediatRCompatTests
{
    // ── Helper ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ServiceProvider"/> with Nexum+MediatR compat registered for the test assembly.
    /// </summary>
    private static ServiceProvider BuildProvider(
        Action<global::Nexum.NexumOptions>? configureNexum = null,
        Action<MediatRServiceConfiguration>? configureMediatR = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexumWithMediatRCompat(
            configureNexum,
            configureMediatR,
            typeof(AddNexumWithMediatRCompatTests).Assembly);

        // Reset dispatcher caches so each test gets a clean slate
        global::Nexum.CommandDispatcher.ResetForTesting();
        global::Nexum.QueryDispatcher.ResetForTesting();

        return services.BuildServiceProvider();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddNexumWithMediatRCompat_RegistersBothDispatchers_ResolvesAllAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();

        // Act — resolve both MediatR and Nexum infrastructure
        var mediator = sp.GetService<IMediator>();
        var sender = sp.GetService<ISender>();
        var commandDispatcher = sp.GetService<ICommandDispatcher>();
        var queryDispatcher = sp.GetService<IQueryDispatcher>();
        var notificationPublisher = sp.GetService<Nexum.Abstractions.INotificationPublisher>();

        // Assert
        mediator.Should().NotBeNull();
        sender.Should().NotBeNull();
        commandDispatcher.Should().NotBeNull();
        queryDispatcher.Should().NotBeNull();
        notificationPublisher.Should().NotBeNull();
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_MediatRHandler_DispatchedViaNexumAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();
        var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act — DualCommand has a MediatR handler (DualCommandMediatRHandler).
        // The MediatRCommandAdapter bridges it to Nexum's ICommandDispatcher.
        string result = await commandDispatcher.DispatchAsync(
            new DualCommand("hello"), CancellationToken.None);

        // Assert
        result.Should().Be("mediatR:hello");
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_NativeNexumHandler_HasPriorityAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();
        var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();

        // Act — NativeCommand has BOTH a MediatR handler AND a native Nexum handler.
        // The native handler wins because ScanAndRegisterNexumHandlers runs after the adapter step,
        // and the native handler uses Add (overriding the TryAdd'd adapter via service resolution order).
        // Actually: native Nexum handler is registered via Add after adapter via TryAdd;
        // the dispatcher resolves using PolymorphicHandlerResolver which picks the first matching registration.
        string result = await commandDispatcher.DispatchAsync(
            new NativeCommand("hello"), CancellationToken.None);

        // Assert
        result.Should().Be("nexum:hello");
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_DualDispatch_BothWorkAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();
        var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();
        var mediator = sp.GetRequiredService<IMediator>();

        var command = new DualCommand("world");

        // Act — same command dispatched via both systems
        string nexumResult = await commandDispatcher.DispatchAsync(command, CancellationToken.None);
        string mediatRResult = await mediator.Send(command, CancellationToken.None);

        // Assert
        nexumResult.Should().Be("mediatR:world");
        mediatRResult.Should().Be("mediatR:world");
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_NotificationAdapter_RegisteredForDualTypesAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();
        var notificationPublisher = sp.GetRequiredService<Nexum.Abstractions.INotificationPublisher>();
        DualNotificationMediatRHandler.LastReceived = null;

        // Act — publish DualNotification; the adapter routes it to the MediatR notification handler
        await notificationPublisher.PublishAsync(
            new DualNotification("ping"),
            strategy: PublishStrategy.Sequential,
            CancellationToken.None);

        // Assert — the MediatR handler was invoked and recorded the message
        DualNotificationMediatRHandler.LastReceived.Should().Be("ping");
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_BehaviorAdapter_WrappsMediatRPipelineBehaviorAsync()
    {
        // Arrange
        await using ServiceProvider sp = BuildProvider();
        var commandDispatcher = sp.GetRequiredService<ICommandDispatcher>();
        DualCommandMediatRBehavior.ExecutedCount = 0;

        // Act — the MediatR IPipelineBehavior is wrapped by MediatRCommandBehaviorAdapter
        string result = await commandDispatcher.DispatchAsync(
            new DualCommandWithBehavior("test"), CancellationToken.None);

        // Assert
        result.Should().Be("behavior+mediatR:test");
        DualCommandMediatRBehavior.ExecutedCount.Should().Be(1);
    }

    [Fact]
    public async Task AddNexumWithMediatRCompat_WithoutMediatRConfig_UsesDefaultsAsync()
    {
        // Arrange — no configureMediatR callback; should register with default MediatR config
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexumWithMediatRCompat(
            assemblies: typeof(AddNexumWithMediatRCompatTests).Assembly);

        global::Nexum.CommandDispatcher.ResetForTesting();
        global::Nexum.QueryDispatcher.ResetForTesting();

        // Act — should not throw
        await using ServiceProvider sp = services.BuildServiceProvider();
        var mediator = sp.GetService<IMediator>();
        var commandDispatcher = sp.GetService<ICommandDispatcher>();

        // Assert
        mediator.Should().NotBeNull();
        commandDispatcher.Should().NotBeNull();
    }

    [Fact]
    public void AddNexumWithMediatRCompat_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act & Assert
        var act = () => services.AddNexumWithMediatRCompat(
            assemblies: typeof(AddNexumWithMediatRCompatTests).Assembly);
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    // ── Test fixture types ───────────────────────────────────────────────────

    // --- DualCommand: implements both ICommand<string> and MediatR.IRequest<string> ---
    // Only has a MediatR handler → adapter is registered and bridges to Nexum dispatch

    public record DualCommand(string Value)
        : ICommand<string>, global::MediatR.IRequest<string>;

    public sealed class DualCommandMediatRHandler
        : global::MediatR.IRequestHandler<DualCommand, string>
    {
        public Task<string> Handle(DualCommand request, CancellationToken cancellationToken)
            => Task.FromResult($"mediatR:{request.Value}");
    }

    // --- NativeCommand: has BOTH a MediatR handler AND a native Nexum handler ---
    // The Nexum native handler wins — it is registered via Add and the dispatcher
    // resolves the last registered ICommandHandler<NativeCommand, string> which is the native one.

    public record NativeCommand(string Value)
        : ICommand<string>, global::MediatR.IRequest<string>;

    public sealed class NativeCommandMediatRHandler
        : global::MediatR.IRequestHandler<NativeCommand, string>
    {
        public Task<string> Handle(NativeCommand request, CancellationToken cancellationToken)
            => Task.FromResult($"mediatR:{request.Value}");
    }

    public sealed class NativeCommandNexumHandler
        : ICommandHandler<NativeCommand, string>
    {
        public ValueTask<string> HandleAsync(NativeCommand command, CancellationToken ct = default)
            => new($"nexum:{command.Value}");
    }

    // --- DualNotification: implements both Nexum.INotification and MediatR.INotification ---

    public record DualNotification(string Message)
        : Nexum.Abstractions.INotification, global::MediatR.INotification;

    public sealed class DualNotificationMediatRHandler
        : global::MediatR.INotificationHandler<DualNotification>
    {
        public static string? LastReceived { get; set; }

        public Task Handle(DualNotification notification, CancellationToken cancellationToken)
        {
            LastReceived = notification.Message;
            return Task.CompletedTask;
        }
    }

    // --- DualCommandWithBehavior: command with a MediatR pipeline behavior ---

    public record DualCommandWithBehavior(string Value)
        : ICommand<string>, global::MediatR.IRequest<string>;

    public sealed class DualCommandWithBehaviorMediatRHandler
        : global::MediatR.IRequestHandler<DualCommandWithBehavior, string>
    {
        public Task<string> Handle(DualCommandWithBehavior request, CancellationToken cancellationToken)
            => Task.FromResult($"mediatR:{request.Value}");
    }

    public sealed class DualCommandMediatRBehavior
        : global::MediatR.IPipelineBehavior<DualCommandWithBehavior, string>
    {
        public static int ExecutedCount { get; set; }

        public async Task<string> Handle(
            DualCommandWithBehavior request,
            global::MediatR.RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            ExecutedCount++;
            string result = await next();
            return $"behavior+{result}";
        }
    }
}
