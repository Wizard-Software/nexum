using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Behaviors;

// SG Tier 2: This behavior is inlined in NexumPipelineRegistry.Dispatch_CreateInvoiceCommand().
//            The Source Generator produces a monomorphized delegate chain — no reflection,
//            no boxing, no virtual dispatch on the hot path.
//
// [BehaviorOrder(1)] — executes outermost (first to enter, last to exit).
[BehaviorOrder(1)]
public sealed class LoggingCommandBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        Console.WriteLine($"  [LoggingBehavior] >>> ENTER {typeof(TCommand).Name}");
        var result = await next(ct).ConfigureAwait(false);
        Console.WriteLine($"  [LoggingBehavior] <<< EXIT  {typeof(TCommand).Name}");
        return result;
    }
}
