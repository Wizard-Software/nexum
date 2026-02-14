namespace Nexum.Abstractions;

/// <summary>
/// Handles exceptions thrown during command dispatch.
/// Exception handlers are side-effect only (logging, metrics, alerts) and must NOT swallow exceptions.
/// The dispatcher always re-throws after invoking exception handlers.
/// </summary>
/// <typeparam name="TCommand">The command type. Contravariant. Constrained to <see cref="ICommand"/> (non-generic marker).</typeparam>
/// <typeparam name="TException">The exception type. Contravariant.</typeparam>
public interface ICommandExceptionHandler<in TCommand, in TException>
    where TCommand : ICommand
    where TException : Exception
{
    /// <summary>
    /// Handles the exception as a side-effect. The original exception is always re-thrown by the dispatcher.
    /// </summary>
    /// <param name="command">The command that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="ct">Optional cancellation token.</param>
    ValueTask HandleAsync(TCommand command, TException exception, CancellationToken ct = default);
}
