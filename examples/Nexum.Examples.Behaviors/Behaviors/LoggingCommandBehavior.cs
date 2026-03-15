using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Behaviors;

[BehaviorOrder(1)]
public sealed class LoggingCommandBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        Console.WriteLine($"  >>> ENTER {typeof(TCommand).Name}");
        var sw = Stopwatch.StartNew();
        var result = await next(ct).ConfigureAwait(false);
        sw.Stop();
        Console.WriteLine($"  <<< EXIT {typeof(TCommand).Name} ({sw.ElapsedMilliseconds}ms)");
        return result;
    }
}
