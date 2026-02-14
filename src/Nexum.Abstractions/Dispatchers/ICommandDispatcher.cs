namespace Nexum.Abstractions;

/// <summary>
/// Dispatches commands to their registered handlers through the command pipeline.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches the specified command to its handler asynchronously.
    /// </summary>
    /// <typeparam name="TResult">The type of result produced by the command.</typeparam>
    /// <param name="command">The command to dispatch.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of the command.</returns>
    ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default);
}
