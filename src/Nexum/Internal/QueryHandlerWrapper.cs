using System.Collections.Concurrent;
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

/// <summary>
/// Non-generic holder for the query behaviors caches — shared across all
/// <see cref="QueryHandlerWrapperImpl{TQuery,TResult}"/> and
/// <see cref="StreamQueryHandlerWrapperImpl{TQuery,TResult}"/> instantiations.
/// Kept separate to allow <see cref="ResetForTesting"/> without type parameters.
/// </summary>
internal static class QueryHandlerWrapperCache
{
    /// <summary>
    /// Cache indicating whether any behaviors are registered per closed query type.
    /// Key: closed generic wrapper type (uniquely identifies TQuery + TResult pair).
    /// Value: true if behaviors exist, false otherwise.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, bool> HasBehaviors = new();

    /// <summary>
    /// Cache indicating whether any behaviors are registered per closed stream query type.
    /// Key: closed generic wrapper type (uniquely identifies TQuery + TResult pair).
    /// Value: true if behaviors exist, false otherwise.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, bool> HasStreamBehaviors = new();

    /// <summary>Clears all caches. For testing purposes only.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetForTesting()
    {
        HasBehaviors.Clear();
        HasStreamBehaviors.Clear();
    }
}

internal sealed class QueryHandlerWrapperImpl<TQuery, TResult> : QueryHandlerWrapper<TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Stable type key for this generic instantiation — used as key in <see cref="QueryHandlerWrapperCache.HasBehaviors"/>.
    /// Using <c>typeof(QueryHandlerWrapperImpl&lt;TQuery, TResult&gt;)</c> as key uniquely identifies
    /// the (TQuery, TResult) pair without boxing.
    /// </summary>
    private static readonly Type s_cacheKey = typeof(QueryHandlerWrapperImpl<TQuery, TResult>);

    private Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>>? _compiledDispatch;

    public override ValueTask<TResult> HandleAsync(
        IQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedQuery = (TQuery)query;

        // Zero-alloc hot path: skip DI lookup entirely when cached as "no behaviors"
        if (QueryHandlerWrapperCache.HasBehaviors.TryGetValue(s_cacheKey, out bool hasBehaviors) && !hasBehaviors)
        {
            var handler = (IQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
                typeof(IQueryHandler<TQuery, TResult>));
            return handler.HandleAsync(typedQuery, ct);
        }

        return HandleWithBehaviorCheckAsync(typedQuery, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
    /// Separated from the hot path to keep the inlineable fast path small.
    /// </summary>
    private ValueTask<TResult> HandleWithBehaviorCheckAsync(
        TQuery typedQuery,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var handler = (IQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
            typeof(IQueryHandler<TQuery, TResult>));

        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<IQueryBehavior<TQuery, TResult>>));

        if (behaviorsObj is null or IQueryBehavior<TQuery, TResult>[] { Length: 0 })
        {
            // No behaviors — cache and return direct handler call
            QueryHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, false);
            return handler.HandleAsync(typedQuery, ct);
        }

        // Behaviors exist — cache and build pipeline
        QueryHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, true);
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
    /// <summary>
    /// Stable type key for this generic instantiation — used as key in <see cref="QueryHandlerWrapperCache.HasStreamBehaviors"/>.
    /// Using <c>typeof(StreamQueryHandlerWrapperImpl&lt;TQuery, TResult&gt;)</c> as key uniquely identifies
    /// the (TQuery, TResult) pair without boxing.
    /// </summary>
    private static readonly Type s_cacheKey = typeof(StreamQueryHandlerWrapperImpl<TQuery, TResult>);

    private Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>>? _compiledDispatch;

    public override IAsyncEnumerable<TResult> HandleAsync(
        IStreamQuery<TResult> query,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedQuery = (TQuery)query;

        // Zero-alloc hot path: skip DI lookup entirely when cached as "no behaviors"
        if (QueryHandlerWrapperCache.HasStreamBehaviors.TryGetValue(s_cacheKey, out bool hasBehaviors) && !hasBehaviors)
        {
            var handler = (IStreamQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
                typeof(IStreamQueryHandler<TQuery, TResult>));
            return handler.HandleAsync(typedQuery, ct);
        }

        return HandleWithBehaviorCheckAsync(typedQuery, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
    /// Separated from the hot path to keep the inlineable fast path small.
    /// </summary>
    private IAsyncEnumerable<TResult> HandleWithBehaviorCheckAsync(
        TQuery typedQuery,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var handler = (IStreamQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
            typeof(IStreamQueryHandler<TQuery, TResult>));

        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<IStreamQueryBehavior<TQuery, TResult>>));

        if (behaviorsObj is null or IStreamQueryBehavior<TQuery, TResult>[] { Length: 0 })
        {
            // No behaviors — cache and return direct handler call
            QueryHandlerWrapperCache.HasStreamBehaviors.TryAdd(s_cacheKey, false);
            return handler.HandleAsync(typedQuery, ct);
        }

        // Behaviors exist — cache and build pipeline
        QueryHandlerWrapperCache.HasStreamBehaviors.TryAdd(s_cacheKey, true);
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
