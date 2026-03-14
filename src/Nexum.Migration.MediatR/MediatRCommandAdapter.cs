using Nexum.Abstractions;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Nexum command handler that delegates to an existing MediatR <see cref="global::MediatR.IRequestHandler{TRequest, TResponse}"/>.
/// Enables gradual migration: the request type implements both <see cref="ICommand{TResult}"/> and <see cref="global::MediatR.IRequest{TResponse}"/>.
/// </summary>
/// <remarks>
/// <para>Performance note: wraps <c>Task&lt;T&gt;</c> (MediatR) in <c>ValueTask&lt;T&gt;</c> (Nexum),
/// which adds an allocation. This is an acceptable trade-off during migration.</para>
/// </remarks>
/// <typeparam name="TRequest">The request type that implements both Nexum ICommand and MediatR IRequest.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public sealed class MediatRCommandAdapter<TRequest, TResult>
    : ICommandHandler<TRequest, TResult>
    where TRequest : ICommand<TResult>, global::MediatR.IRequest<TResult>
{
    private readonly global::MediatR.IRequestHandler<TRequest, TResult> _mediatRHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="MediatRCommandAdapter{TRequest, TResult}"/>.
    /// </summary>
    /// <param name="mediatRHandler">The MediatR request handler to delegate to.</param>
    public MediatRCommandAdapter(global::MediatR.IRequestHandler<TRequest, TResult> mediatRHandler)
    {
        ArgumentNullException.ThrowIfNull(mediatRHandler);
        _mediatRHandler = mediatRHandler;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> HandleAsync(TRequest command, CancellationToken ct = default)
    {
        return await _mediatRHandler.Handle(command, ct).ConfigureAwait(false);
    }
}
