using Nexum.Abstractions;

namespace Nexum.Extensions.DependencyInjection.Tests.TestFixtures;

// Commands — unique per test category to avoid PolymorphicHandlerResolver cache collision
public sealed record AutoDetectionCommand(string Value) : ICommand<string>;

public sealed record BehaviorOrderCommand(string Value) : ICommand<string>;

public sealed record LifetimeCommand(string Value) : ICommand<string>;

// Handlers
public sealed class AutoDetectionHandler : ICommandHandler<AutoDetectionCommand, string>
{
    public ValueTask<string> HandleAsync(AutoDetectionCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(command.Value);
}

public sealed class BehaviorOrderHandler(List<string> tracker) : ICommandHandler<BehaviorOrderCommand, string>
{
    public ValueTask<string> HandleAsync(BehaviorOrderCommand command, CancellationToken ct = default)
    {
        tracker.Add("Handler");
        return ValueTask.FromResult(command.Value);
    }
}

public sealed class LifetimeHandler : ICommandHandler<LifetimeCommand, string>
{
    public ValueTask<string> HandleAsync(LifetimeCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(command.Value);
}

[HandlerLifetime(NexumLifetime.Singleton)]
public sealed class SingletonLifetimeHandler : ICommandHandler<LifetimeCommand, string>
{
    public ValueTask<string> HandleAsync(LifetimeCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(command.Value);
}

// Behaviors — open generic for DI registration
public sealed class TrackingBehaviorA<TCommand, TResult>(List<string> tracker) : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        tracker.Add("BehaviorA:Before");
        var result = await next(ct);
        tracker.Add("BehaviorA:After");
        return result;
    }
}

public sealed class TrackingBehaviorB<TCommand, TResult>(List<string> tracker) : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        tracker.Add("BehaviorB:Before");
        var result = await next(ct);
        tracker.Add("BehaviorB:After");
        return result;
    }
}

public sealed class TrackingBehaviorC<TCommand, TResult>(List<string> tracker) : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        tracker.Add("BehaviorC:Before");
        var result = await next(ct);
        tracker.Add("BehaviorC:After");
        return result;
    }
}

[BehaviorOrder(1)]
public sealed class OrderedBehaviorWithAttribute<TCommand, TResult>(List<string> tracker) : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct = default)
    {
        tracker.Add("OrderedAttr1:Before");
        var result = await next(ct);
        tracker.Add("OrderedAttr1:After");
        return result;
    }
}
