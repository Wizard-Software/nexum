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
    /// Cache of pipeline factories per (closed query type, NexumOptions instance) pair.
    /// Key: <c>(wrapper type, NexumOptions reference)</c> — the NexumOptions reference ensures that
    ///      different DI containers (e.g. in tests) produce separate cache entries and do not share
    ///      factories that bake in different behavior order overrides.
    /// Value: an opaque object holding the typed factory delegate.
    /// Populated on the cold path (first dispatch with behaviors).
    /// </summary>
    internal static readonly ConcurrentDictionary<(Type WrapperType, NexumOptions Options), object> PipelineFactories = new();

    /// <summary>
    /// Cache indicating whether any behaviors are registered per closed stream query type.
    /// Key: closed generic wrapper type (uniquely identifies TQuery + TResult pair).
    /// Value: true if behaviors exist, false otherwise.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, bool> HasStreamBehaviors = new();

    /// <summary>
    /// Cache of pipeline factories per (closed stream query type, NexumOptions instance) pair.
    /// Key: <c>(wrapper type, NexumOptions reference)</c> — the NexumOptions reference ensures that
    ///      different DI containers (e.g. in tests) produce separate cache entries.
    /// Value: an opaque object holding the typed factory delegate.
    /// Populated on the cold path (first dispatch with behaviors).
    /// </summary>
    internal static readonly ConcurrentDictionary<(Type WrapperType, NexumOptions Options), object> StreamPipelineFactories = new();

    /// <summary>Clears all caches. For testing purposes only.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetForTesting()
    {
        HasBehaviors.Clear();
        PipelineFactories.Clear();
        HasStreamBehaviors.Clear();
        StreamPipelineFactories.Clear();
    }
}

internal sealed class QueryHandlerWrapperImpl<TQuery, TResult> : QueryHandlerWrapper<TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Stable type key for this generic instantiation — used as key in
    /// <see cref="QueryHandlerWrapperCache.HasBehaviors"/> and
    /// <see cref="QueryHandlerWrapperCache.PipelineFactories"/>.
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

        // Pipeline-factory hot path: behaviors exist and factory is cached — skip sorting.
        // Key includes NexumOptions instance so different DI containers use separate factories.
        var factoryKey = (s_cacheKey, options);
        if (hasBehaviors
            && QueryHandlerWrapperCache.PipelineFactories.TryGetValue(factoryKey, out object? factoryObj)
            && factoryObj is Func<TQuery, IQueryHandler<TQuery, TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>> factory)
        {
            var handler = (IQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
                typeof(IQueryHandler<TQuery, TResult>));
            return factory(typedQuery, handler, serviceProvider, ct);
        }

        return HandleWithBehaviorCheckAsync(typedQuery, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
    /// On first dispatch with behaviors, builds a pipeline factory capturing the sorted order and
    /// stores it in <see cref="QueryHandlerWrapperCache.PipelineFactories"/> for subsequent dispatches.
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

        // Behaviors exist — cache has-behaviors flag
        QueryHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, true);
        var behaviors = (IQueryBehavior<TQuery, TResult>[])behaviorsObj;

        // Build pipeline for this dispatch AND capture the sorted order for factory creation.
        QueryHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildQueryPipelineAndCaptureSorted(
            typedQuery, handler, behaviors, options, out IQueryBehavior<TQuery, TResult>[] sortedBehaviors);

        // Store factory only if not already present.
        // Key includes options reference — different DI containers use separate factory entries.
        QueryHandlerWrapperCache.PipelineFactories.TryAdd(
            (s_cacheKey, options),
            PipelineBuilder.BuildQueryPipelineFactory<TQuery, TResult>(sortedBehaviors));

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
    /// Stable type key for this generic instantiation — used as key in
    /// <see cref="QueryHandlerWrapperCache.HasStreamBehaviors"/> and
    /// <see cref="QueryHandlerWrapperCache.StreamPipelineFactories"/>.
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

        // Pipeline-factory hot path: behaviors exist and factory is cached — skip sorting.
        // Key includes NexumOptions instance so different DI containers use separate factories.
        var factoryKey = (s_cacheKey, options);
        if (hasBehaviors
            && QueryHandlerWrapperCache.StreamPipelineFactories.TryGetValue(factoryKey, out object? factoryObj)
            && factoryObj is Func<TQuery, IStreamQueryHandler<TQuery, TResult>, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> factory)
        {
            var handler = (IStreamQueryHandler<TQuery, TResult>)serviceProvider.GetRequiredService(
                typeof(IStreamQueryHandler<TQuery, TResult>));
            return factory(typedQuery, handler, serviceProvider, ct);
        }

        return HandleWithBehaviorCheckAsync(typedQuery, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
    /// On first dispatch with behaviors, builds a pipeline factory capturing the sorted order and
    /// stores it in <see cref="QueryHandlerWrapperCache.StreamPipelineFactories"/> for subsequent dispatches.
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

        // Behaviors exist — cache has-behaviors flag
        QueryHandlerWrapperCache.HasStreamBehaviors.TryAdd(s_cacheKey, true);
        var behaviors = (IStreamQueryBehavior<TQuery, TResult>[])behaviorsObj;

        // Build pipeline for this dispatch AND capture the sorted order for factory creation.
        StreamQueryHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildStreamQueryPipelineAndCaptureSorted(
            typedQuery, handler, behaviors, options, out IStreamQueryBehavior<TQuery, TResult>[] sortedBehaviors);

        // Store factory only if not already present.
        // Key includes options reference — different DI containers use separate factory entries.
        QueryHandlerWrapperCache.StreamPipelineFactories.TryAdd(
            (s_cacheKey, options),
            PipelineBuilder.BuildStreamQueryPipelineFactory<TQuery, TResult>(sortedBehaviors));

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
