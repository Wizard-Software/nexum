using Nexum.Abstractions;
using MediatRNS = global::MediatR;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Nexum command behavior that delegates to an existing MediatR <see cref="global::MediatR.IPipelineBehavior{TRequest, TResponse}"/>.
/// Converts between Nexum's <see cref="CommandHandlerDelegate{TResult}"/> and MediatR's <see cref="global::MediatR.RequestHandlerDelegate{TResponse}"/>.
/// </summary>
/// <typeparam name="TCommand">The command type that implements both Nexum ICommand and MediatR IRequest.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public sealed class MediatRCommandBehaviorAdapter<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>, MediatRNS.IRequest<TResult>
{
    private readonly MediatRNS.IPipelineBehavior<TCommand, TResult> _mediatRBehavior;

    /// <summary>
    /// Initializes a new instance of <see cref="MediatRCommandBehaviorAdapter{TCommand, TResult}"/>.
    /// </summary>
    /// <param name="mediatRBehavior">The MediatR pipeline behavior to wrap.</param>
    public MediatRCommandBehaviorAdapter(MediatRNS.IPipelineBehavior<TCommand, TResult> mediatRBehavior)
    {
        ArgumentNullException.ThrowIfNull(mediatRBehavior);
        _mediatRBehavior = mediatRBehavior;
    }

    /// <inheritdoc />
    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        MediatRNS.RequestHandlerDelegate<TResult> mediatRNext =
            () => next(ct).AsTask();

        return await _mediatRBehavior.Handle(command, mediatRNext, ct).ConfigureAwait(false);
    }
}
