namespace Nexum.Abstractions;

/// <summary>
/// Defines a pipeline behavior that wraps command execution (Russian doll model).
/// Behaviors execute in order defined by BehaviorOrderAttribute or DI registration order.
/// </summary>
/// <typeparam name="TCommand">The command type. Contravariant.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommandBehavior<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the command, optionally delegating to the next behavior or handler via <paramref name="next"/>.
    /// </summary>
    /// <param name="command">The command being dispatched.</param>
    /// <param name="next">The next step in the pipeline.</param>
    /// <param name="ct">Optional cancellation token.</param>
    ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default);
}
