using System.Reflection;
using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Internal;

internal abstract class QueryHandlerWrapper<TResult>
{
    public abstract ValueTask<TResult> HandleAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct);

    /// <summary>
    /// Creates and caches a strongly-typed delegate from the compiled pipeline MethodInfo.
    /// Eliminates MethodInfo.Invoke overhead on Tier 2 (SG) path.
    /// </summary>
    public abstract void SetCompiledDelegate(MethodInfo method);

    /// <summary>
    /// Invokes the cached compiled pipeline delegate directly (zero reflection).
    /// Must only be called after <see cref="SetCompiledDelegate"/> has been called.
    /// </summary>
    public abstract ValueTask<TResult> InvokeCompiledAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct);

    /// <summary>
    /// Whether a compiled delegate has been cached for this wrapper.
    /// </summary>
    public bool HasCompiledDelegate { get; protected set; }
}

internal sealed class QueryHandlerWrapperImpl<TQuery, TResult> : QueryHandlerWrapper<TResult>
    where TQuery : IQuery<TResult>
{
    private Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>>? _compiledDispatch;

    public override ValueTask<TResult> HandleAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedQuery = (TQuery)query;
        var handler = (IQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
            typeof(IQueryHandler<TQuery, TResult>));

        // Fast path: no behaviors registered — direct handler call, zero allocations, zero async SM
        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<IQueryBehavior<TQuery, TResult>>));
        if (behaviorsObj is null or IQueryBehavior<TQuery, TResult>[] { Length: 0 })
        {
            return handler.HandleAsync(typedQuery, ct);
        }

        // Slow path: pass already-resolved behaviors to PipelineBuilder (no second DI lookup)
        var behaviors = (IEnumerable<IQueryBehavior<TQuery, TResult>>)behaviorsObj;
        QueryHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildQueryPipeline(
            typedQuery, handler, behaviors, options);
        return pipeline(ct);
    }

    public override void SetCompiledDelegate(MethodInfo method)
    {
        _compiledDispatch = method.CreateDelegate<
            Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>>>();
        HasCompiledDelegate = true;
    }

    public override ValueTask<TResult> InvokeCompiledAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        return _compiledDispatch!((TQuery)query, serviceProvider, ct);
    }
}

internal abstract class StreamQueryHandlerWrapper<TResult>
{
    public abstract IAsyncEnumerable<TResult> HandleAsync(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct);

    /// <summary>
    /// Creates and caches a strongly-typed delegate from the compiled pipeline MethodInfo.
    /// Eliminates MethodInfo.Invoke overhead on Tier 2 (SG) path.
    /// </summary>
    public abstract void SetCompiledDelegate(MethodInfo method);

    /// <summary>
    /// Invokes the cached compiled pipeline delegate directly (zero reflection).
    /// Must only be called after <see cref="SetCompiledDelegate"/> has been called.
    /// </summary>
    public abstract IAsyncEnumerable<TResult> InvokeCompiledAsync(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct);

    /// <summary>
    /// Whether a compiled delegate has been cached for this wrapper.
    /// </summary>
    public bool HasCompiledDelegate { get; protected set; }
}

internal sealed class StreamQueryHandlerWrapperImpl<TQuery, TResult> : StreamQueryHandlerWrapper<TResult>
    where TQuery : IStreamQuery<TResult>
{
    private Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>>? _compiledDispatch;

    public override IAsyncEnumerable<TResult> HandleAsync(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedQuery = (TQuery)query;
        var handler = (IStreamQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
            typeof(IStreamQueryHandler<TQuery, TResult>));

        // Fast path: no behaviors registered — direct handler call, zero allocations
        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<IStreamQueryBehavior<TQuery, TResult>>));
        if (behaviorsObj is null or IStreamQueryBehavior<TQuery, TResult>[] { Length: 0 })
        {
            return handler.HandleAsync(typedQuery, ct);
        }

        // Slow path: pass already-resolved behaviors to PipelineBuilder (no second DI lookup)
        var behaviors = (IEnumerable<IStreamQueryBehavior<TQuery, TResult>>)behaviorsObj;
        StreamQueryHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildStreamQueryPipeline(
            typedQuery, handler, behaviors, options);
        return pipeline(ct);
    }

    public override void SetCompiledDelegate(MethodInfo method)
    {
        _compiledDispatch = method.CreateDelegate<
            Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>>>();
        HasCompiledDelegate = true;
    }

    public override IAsyncEnumerable<TResult> InvokeCompiledAsync(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        return _compiledDispatch!((TQuery)query, serviceProvider, ct);
    }
}
