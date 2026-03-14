using Nexum.Abstractions;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Nexum query handler that delegates to an existing MediatR <see cref="global::MediatR.IRequestHandler{TRequest, TResponse}"/>.
/// Enables gradual migration: the request type implements both <see cref="IQuery{TResult}"/> and <see cref="global::MediatR.IRequest{TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type that implements both Nexum IQuery and MediatR IRequest.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public sealed class MediatRQueryAdapter<TRequest, TResult>
    : IQueryHandler<TRequest, TResult>
    where TRequest : IQuery<TResult>, global::MediatR.IRequest<TResult>
{
    private readonly global::MediatR.IRequestHandler<TRequest, TResult> _mediatRHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="MediatRQueryAdapter{TRequest, TResult}"/>.
    /// </summary>
    /// <param name="mediatRHandler">The MediatR request handler to delegate to.</param>
    public MediatRQueryAdapter(global::MediatR.IRequestHandler<TRequest, TResult> mediatRHandler)
    {
        ArgumentNullException.ThrowIfNull(mediatRHandler);
        _mediatRHandler = mediatRHandler;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> HandleAsync(TRequest query, CancellationToken ct = default)
    {
        return await _mediatRHandler.Handle(query, ct).ConfigureAwait(false);
    }
}
