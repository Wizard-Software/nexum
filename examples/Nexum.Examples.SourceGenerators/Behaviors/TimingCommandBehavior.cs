using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Behaviors;

// SG Tier 2: This behavior is also inlined in the compiled pipeline delegate.
//            Pipeline order: LoggingBehavior(1) → TimingBehavior(2) → Handler.
//
// [BehaviorOrder(2)] — executes after LoggingBehavior (inner wrap).
[BehaviorOrder(2)]
public sealed class TimingCommandBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = await next(ct).ConfigureAwait(false);
        sw.Stop();
        Console.WriteLine($"  [TimingBehavior]  {typeof(TCommand).Name} completed in {sw.ElapsedMilliseconds}ms");
        return result;
    }
}
