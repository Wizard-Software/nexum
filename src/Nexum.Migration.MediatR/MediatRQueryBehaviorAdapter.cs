using Nexum.Abstractions;
using MediatRNS = global::MediatR;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Nexum query behavior that delegates to an existing MediatR <see cref="global::MediatR.IPipelineBehavior{TRequest, TResponse}"/>.
/// Converts between Nexum's <see cref="QueryHandlerDelegate{TResult}"/> and MediatR's <see cref="global::MediatR.RequestHandlerDelegate{TResponse}"/>.
/// </summary>
/// <typeparam name="TQuery">The query type that implements both Nexum IQuery and MediatR IRequest.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public sealed class MediatRQueryBehaviorAdapter<TQuery, TResult>
    : IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>, MediatRNS.IRequest<TResult>
{
    private readonly MediatRNS.IPipelineBehavior<TQuery, TResult> _mediatRBehavior;

    /// <summary>
    /// Initializes a new instance of <see cref="MediatRQueryBehaviorAdapter{TQuery, TResult}"/>.
    /// </summary>
    /// <param name="mediatRBehavior">The MediatR pipeline behavior to wrap.</param>
    public MediatRQueryBehaviorAdapter(MediatRNS.IPipelineBehavior<TQuery, TResult> mediatRBehavior)
    {
        ArgumentNullException.ThrowIfNull(mediatRBehavior);
        _mediatRBehavior = mediatRBehavior;
    }

    /// <inheritdoc />
    public async ValueTask<TResult> HandleAsync(
        TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        MediatRNS.RequestHandlerDelegate<TResult> mediatRNext =
            () => next(ct).AsTask();

        return await _mediatRBehavior.Handle(query, mediatRNext, ct).ConfigureAwait(false);
    }
}
