namespace Nexum.Abstractions;

/// <summary>
/// Defines a handler for commands of type <typeparamref name="TCommand"/>
/// that produce a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TCommand">The command type to handle. Contravariant.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Handles the specified command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the result of handling the command.</returns>
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
