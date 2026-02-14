using System.ComponentModel;
using Nexum.Abstractions;

namespace Nexum;

/// <summary>
/// Internal interface used by Source Generator Tier 3 interceptors.
/// Do not implement or call directly.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IInterceptableDispatcher
{
    /// <summary>
    /// Executes a compiled command pipeline with full infrastructure
    /// (null check, depth guard, exception handling, async elision).
    /// </summary>
    ValueTask<TResult> DispatchInterceptedCommandAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TCommand : ICommand<TResult>;

    /// <summary>
    /// Executes a compiled query pipeline with full infrastructure.
    /// </summary>
    ValueTask<TResult> DispatchInterceptedQueryAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TQuery : IQuery<TResult>;

    /// <summary>
    /// Executes a compiled stream query pipeline with full infrastructure.
    /// </summary>
    IAsyncEnumerable<TResult> StreamInterceptedAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> compiledPipeline,
        CancellationToken ct)
        where TQuery : IStreamQuery<TResult>;
}
