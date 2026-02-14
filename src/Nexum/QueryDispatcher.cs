using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum;

/// <summary>
/// Default implementation of <see cref="IQueryDispatcher"/> that dispatches queries to their handlers
/// through a behavior pipeline with exception handling support.
/// </summary>
/// <remarks>
/// <para>
/// This dispatcher is thread-safe and designed to be registered as a singleton.
/// It uses cached handler wrappers for the runtime path (without Source Generator)
/// that eliminate per-dispatch reflection overhead.
/// </para>
/// <para>
/// The dispatch flow for <see cref="DispatchAsync{TResult}"/>:
/// </para>
/// <list type="number">
/// <item>Validate dispatch depth to prevent stack overflow in recursive scenarios</item>
/// <item>Resolve the handler type polymorphically (supports base class handlers)</item>
/// <item>Obtain a cached wrapper and build the behavior pipeline (Russian doll pattern)</item>
/// <item>Execute the pipeline and invoke exception handlers on failure (always re-throws)</item>
/// </list>
/// <para>
/// The dispatch flow for <see cref="StreamAsync{TResult}"/>:
/// </para>
/// <list type="number">
/// <item>Validate dispatch depth during setup (synchronous)</item>
/// <item>Resolve the handler type polymorphically (supports base class handlers)</item>
/// <item>Obtain a cached wrapper and build the streaming behavior pipeline (Russian doll pattern)</item>
/// <item>Return the <see cref="IAsyncEnumerable{TResult}"/> (no exception handlers per design N8)</item>
/// </list>
/// </remarks>
public sealed class QueryDispatcher : IQueryDispatcher, IInterceptableDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NexumOptions _options;
    private readonly ExceptionHandlerResolver _exceptionHandlerResolver;

    /// <summary>
    /// Cache of query handler wrappers for the runtime path.
    /// </summary>
    /// <remarks>
    /// Key: queryType (runtime type of the query)
    /// Value: <see cref="QueryHandlerWrapper{TResult}"/> (stored as object because TResult varies)
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Wrapper creation is idempotent and stateless — benign race on first access is harmless.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, object> s_queryWrapperCache = new();

    /// <summary>
    /// Cache of stream query handler wrappers for the runtime path.
    /// </summary>
    /// <remarks>
    /// Key: queryType (runtime type of the stream query)
    /// Value: <see cref="StreamQueryHandlerWrapper{TResult}"/> (stored as object because TResult varies)
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Wrapper creation is idempotent and stateless — benign race on first access is harmless.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, object> s_streamWrapperCache = new();

    /// <summary>
    /// Cache of compiled query pipeline MethodInfo from NexumPipelineRegistry (Tier 2).
    /// </summary>
    /// <remarks>
    /// Key: queryType
    /// Value: MethodInfo for the compiled dispatch method, or null if not compiled
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, MethodInfo?> s_compiledQueryMethodCache = new();

    /// <summary>
    /// Cache of compiled stream query pipeline MethodInfo from NexumPipelineRegistry (Tier 2).
    /// </summary>
    /// <remarks>
    /// Key: queryType
    /// Value: MethodInfo for the compiled stream method, or null if not compiled
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, MethodInfo?> s_compiledStreamMethodCache = new();

    /// <summary>
    /// Cache of Tier 2 query dispatch delegates per query type.
    /// Stores typed Func delegates that invoke compiled pipeline directly (zero lambda closure per dispatch).
    /// </summary>
    /// <remarks>
    /// Key: queryType
    /// Value: Func&lt;IQuery&lt;TResult&gt;, IServiceProvider, CancellationToken, ValueTask&lt;TResult&gt;&gt; (stored as object because TResult varies)
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Only populated when compiled pipeline exists AND no runtime behaviors are registered.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, object> s_tier2QueryDelegateCache = new();

    /// <summary>
    /// Cache indicating whether a query type has any runtime (non-compiled) behaviors registered.
    /// When false, the Tier 2 fast path can skip WrapQueryWithRuntimeBehaviors entirely.
    /// DI container is immutable after build — this cache is safe for the application lifetime.
    /// </summary>
    /// <remarks>
    /// Key: queryType
    /// Value: true if runtime behaviors exist, false otherwise
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Cached on first dispatch per query type (benign race).
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, bool> s_hasRuntimeQueryBehaviorsCache = new();

    // Tier 2: Set in constructor from NexumOptions
    private readonly Type? _pipelineRegistryType;
    private readonly Func<Type, bool>? _isCompiledBehavior;
    private readonly Lazy<bool> _hasCompiledBehaviorOverrides;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers and behaviors.</param>
    /// <param name="options">The Nexum configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="options"/> is null.</exception>
    public QueryDispatcher(IServiceProvider serviceProvider, NexumOptions options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _exceptionHandlerResolver = serviceProvider.GetRequiredService<ExceptionHandlerResolver>();

        // Tier 2: Setup compiled pipeline detection
        _pipelineRegistryType = options.PipelineRegistryType;
        if (_pipelineRegistryType is not null)
        {
            // Cache the IsCompiledBehavior method as a delegate
            MethodInfo? isCompiledMethod = _pipelineRegistryType.GetMethod("IsCompiledBehavior",
                BindingFlags.Public | BindingFlags.Static);
            if (isCompiledMethod is not null)
            {
                _isCompiledBehavior = (Func<Type, bool>)isCompiledMethod.CreateDelegate(typeof(Func<Type, bool>));
            }
        }

        // Lazy-initialize ADR-006 guard check
        _hasCompiledBehaviorOverrides = new Lazy<bool>(() => CheckCompiledBehaviorOverrides());
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_options.MaxDispatchDepth < int.MaxValue)
        {
            return DispatchWithDepthGuardAsync(query, ct);
        }

        if (_pipelineRegistryType is not null)
        {
            return DispatchTier2FastAsync(query, ct);
        }

        return DispatchRuntimeFastAsync(query, ct);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IStreamQuery<TResult> query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Step 1: Validate dispatch depth (re-entrant guard) — skip when effectively disabled
        // Note: Depth guard disposes before method returns (synchronous setup only)
        DispatchDepthGuard.DepthGuardScope depthGuardScope = default;
        bool depthGuardActive = _options.MaxDispatchDepth < int.MaxValue;
        try
        {
            if (depthGuardActive)
            {
                depthGuardScope = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);
            }

            Type queryType = query.GetType();

            // Tier 2: Try compiled pipeline first
            if (_pipelineRegistryType is not null
                && _isCompiledBehavior is not null
                && !_hasCompiledBehaviorOverrides.Value)
            {
                MethodInfo? compiledMethod = GetCompiledStreamQueryMethod(queryType);
                if (compiledMethod is not null)
                {
                    // Use wrapper's cached delegate (zero reflection after cold path)
                    StreamQueryHandlerWrapper<TResult> compiledWrapper = GetOrCreateStreamWrapper<TResult>(queryType);
                    if (!compiledWrapper.HasCompiledDelegate)
                    {
                        compiledWrapper.SetCompiledDelegate(compiledMethod);
                    }

                    StreamQueryHandlerDelegate<TResult> compiledPipeline =
                        innerCt => compiledWrapper.InvokeCompiledAsync(query, _serviceProvider, innerCt);

                    StreamQueryHandlerDelegate<TResult> fullPipeline = PipelineBuilder.WrapStreamQueryWithRuntimeBehaviors(
                        _serviceProvider, query, compiledPipeline, _options, _isCompiledBehavior);

                    return fullPipeline(ct);
                }
            }

            // Runtime path — TryGetValue fast path (skip PolymorphicHandlerResolver on steady-state)
            StreamQueryHandlerWrapper<TResult> wrapper = GetOrCreateStreamWrapper<TResult>(queryType);

            // Return the streaming pipeline (no exception handlers per design N8)
            return wrapper.HandleAsync(query, _serviceProvider, _options, ct);
        }
        finally
        {
            if (depthGuardActive)
            {
                depthGuardScope.Dispose();
            }
        }
    }

    /// <summary>
    /// Full async path with depth guard scope (default configuration: MaxDispatchDepth = 16).
    /// Handles both Tier 2 (compiled pipeline) and Runtime (wrapper) paths.
    /// </summary>
    private async ValueTask<TResult> DispatchWithDepthGuardAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken ct)
    {
        using DispatchDepthGuard.DepthGuardScope depthGuard = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);

        Type queryType = query.GetType();

        try
        {
            // Tier 2: Try compiled pipeline first
            if (_pipelineRegistryType is not null
                && _isCompiledBehavior is not null
                && !_hasCompiledBehaviorOverrides.Value)
            {
                MethodInfo? compiledMethod = GetCompiledQueryMethod(queryType);
                if (compiledMethod is not null)
                {
                    // Use wrapper's cached delegate (zero reflection after cold path)
                    QueryHandlerWrapper<TResult> compiledWrapper = GetOrCreateQueryWrapper<TResult>(queryType);
                    if (!compiledWrapper.HasCompiledDelegate)
                    {
                        compiledWrapper.SetCompiledDelegate(compiledMethod);
                    }

                    // Check if runtime behaviors exist (cached per query type)
                    if (s_hasRuntimeQueryBehaviorsCache.TryGetValue(queryType, out bool hasRtBehaviors) && !hasRtBehaviors)
                    {
                        // No runtime behaviors — invoke compiled delegate directly
                        return await compiledWrapper.InvokeCompiledAsync(query, _serviceProvider, ct)
                            .ConfigureAwait(false);
                    }

                    // Has runtime behaviors OR first dispatch (not cached yet) — use WrapQuery
                    QueryHandlerDelegate<TResult> compiledPipeline =
                        innerCt => compiledWrapper.InvokeCompiledAsync(query, _serviceProvider, innerCt);
                    QueryHandlerDelegate<TResult> fullPipeline = PipelineBuilder.WrapQueryWithRuntimeBehaviors(
                        _serviceProvider, query, compiledPipeline, _options, _isCompiledBehavior);

                    // Cache the runtime behaviors flag on first dispatch
                    s_hasRuntimeQueryBehaviorsCache.TryAdd(queryType, !ReferenceEquals(compiledPipeline, fullPipeline));

                    return await fullPipeline(ct).ConfigureAwait(false);
                }
            }

            // Runtime path — TryGetValue fast path (skip PolymorphicHandlerResolver on steady-state)
            QueryHandlerWrapper<TResult> wrapper = GetOrCreateQueryWrapper<TResult>(queryType);
            return await wrapper.HandleAsync(query, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Non-async fast path for Tier 2 (SG compiled pipeline) without depth guard.
    /// Avoids async state machine allocation on synchronously completing handlers.
    /// </summary>
    private ValueTask<TResult> DispatchTier2FastAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken ct)
    {
        Type queryType = query.GetType();

        // Hot path: try cached Tier 2 delegate first
        if (s_tier2QueryDelegateCache.TryGetValue(queryType, out object? cached))
        {
            var del = (Func<IQuery<TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>)cached;
            try
            {
                ValueTask<TResult> task = del(query, _serviceProvider, ct);
                if (task.IsCompletedSuccessfully)
                {
                    return task;
                }

                return AwaitWithExceptionHandlingAsync(query, task, ct);
            }
            catch (Exception ex)
            {
                return HandleSyncExceptionAsync<TResult>(query, ex, ct);
            }
        }

        // Cold path: setup and cache
        return DispatchTier2ColdAsync(query, queryType, ct);
    }

    /// <summary>
    /// Cold path for Tier 2 query dispatch: resolves compiled method, caches wrapper and delegate if no runtime behaviors.
    /// Falls back to runtime wrapper if no compiled method is found.
    /// </summary>
    private async ValueTask<TResult> DispatchTier2ColdAsync<TResult>(
        IQuery<TResult> query,
        Type queryType,
        CancellationToken ct)
    {
        try
        {
            if (_isCompiledBehavior is not null
                && !_hasCompiledBehaviorOverrides.Value)
            {
                MethodInfo? compiledMethod = GetCompiledQueryMethod(queryType);
                if (compiledMethod is not null)
                {
                    // Setup wrapper's cached delegate (zero reflection after cold path)
                    QueryHandlerWrapper<TResult> compiledWrapper = GetOrCreateQueryWrapper<TResult>(queryType);
                    if (!compiledWrapper.HasCompiledDelegate)
                    {
                        compiledWrapper.SetCompiledDelegate(compiledMethod);
                    }

                    QueryHandlerDelegate<TResult> compiledPipeline =
                        innerCt => compiledWrapper.InvokeCompiledAsync(query, _serviceProvider, innerCt);
                    QueryHandlerDelegate<TResult> fullPipeline = PipelineBuilder.WrapQueryWithRuntimeBehaviors(
                        _serviceProvider, query, compiledPipeline, _options, _isCompiledBehavior);

                    // Check if runtime behaviors exist (identity check: compiledPipeline == fullPipeline means no runtime behaviors)
                    bool hasRuntimeBehaviors = !ReferenceEquals(compiledPipeline, fullPipeline);
                    s_hasRuntimeQueryBehaviorsCache.TryAdd(queryType, hasRuntimeBehaviors);

                    if (!hasRuntimeBehaviors)
                    {
                        // Fast path: no runtime behaviors — cache delegate for future hot-path use
                        QueryHandlerWrapper<TResult> cachedWrapper = compiledWrapper;
                        s_tier2QueryDelegateCache.TryAdd(queryType,
                            (Func<IQuery<TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>)
                            ((qry, sp, token) => cachedWrapper.InvokeCompiledAsync(qry, sp, token)));
                    }

                    return await fullPipeline(ct).ConfigureAwait(false);
                }
            }

            // Fallback to runtime wrapper
            QueryHandlerWrapper<TResult> wrapper = GetOrCreateQueryWrapper<TResult>(queryType);
            return await wrapper.HandleAsync(query, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Non-async fast path for runtime query dispatch (no depth guard, no Tier 2).
    /// Avoids async state machine allocation on synchronously completing handlers.
    /// </summary>
    private ValueTask<TResult> DispatchRuntimeFastAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken ct)
    {
        Type queryType = query.GetType();

        // Hot path: cached wrapper lookup (single ConcurrentDict read)
        if (!s_queryWrapperCache.TryGetValue(queryType, out object? cachedWrapper))
        {
            return DispatchRuntimeColdAsync(query, queryType, ct);
        }

        var wrapper = (QueryHandlerWrapper<TResult>)cachedWrapper;

        try
        {
            ValueTask<TResult> task = wrapper.HandleAsync(query, _serviceProvider, _options, ct);
            if (task.IsCompletedSuccessfully)
            {
                return task;
            }

            return AwaitWithExceptionHandlingAsync(query, task, ct);
        }
        catch (Exception ex)
        {
            return HandleSyncExceptionAsync<TResult>(query, ex, ct);
        }
    }

    /// <summary>
    /// Cold path for runtime query dispatch: resolves handler type, caches wrapper, then dispatches.
    /// </summary>
    private async ValueTask<TResult> DispatchRuntimeColdAsync<TResult>(
        IQuery<TResult> query,
        Type queryType,
        CancellationToken ct)
    {
        try
        {
            QueryHandlerWrapper<TResult> wrapper = GetOrCreateQueryWrapper<TResult>(queryType);
            return await wrapper.HandleAsync(query, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Awaits an asynchronously completing handler and wraps with exception handler invocation.
    /// </summary>
    private async ValueTask<TResult> AwaitWithExceptionHandlingAsync<TResult>(
        IQuery<TResult> query,
        ValueTask<TResult> task,
        CancellationToken ct)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Handles a synchronous exception from non-async dispatch path by invoking exception handlers
    /// and re-throwing with preserved stack trace.
    /// </summary>
    private async ValueTask<TResult> HandleSyncExceptionAsync<TResult>(
        IQuery<TResult> query,
        Exception ex,
        CancellationToken ct)
    {
        await _exceptionHandlerResolver
            .InvokeQueryExceptionHandlersAsync(query, ex, ct)
            .ConfigureAwait(false);
        ExceptionDispatchInfo.Throw(ex); // Preserves original stack trace
        return default!; // Unreachable — ExceptionDispatchInfo.Throw always throws
    }

    /// <summary>
    /// Gets a cached query wrapper or creates one by resolving the handler type via PolymorphicHandlerResolver.
    /// Uses TryGetValue on hot path (single dict lookup), PolymorphicHandlerResolver only on cold path.
    /// </summary>
    private QueryHandlerWrapper<TResult> GetOrCreateQueryWrapper<TResult>(Type queryType)
    {
        if (s_queryWrapperCache.TryGetValue(queryType, out object? cachedWrapper))
        {
            return (QueryHandlerWrapper<TResult>)cachedWrapper;
        }

        Type handlerType = PolymorphicHandlerResolver.Resolve(
            queryType, typeof(IQueryHandler<,>), _serviceProvider)
            ?? throw new NexumHandlerNotFoundException(queryType, "IQueryHandler");

        return (QueryHandlerWrapper<TResult>)s_queryWrapperCache.GetOrAdd(
            queryType,
            static (_, ht) =>
            {
                Type[] genericArgs = ht.GetGenericArguments();
                Type tQuery = genericArgs[0];
                Type tResult = genericArgs[1];
                Type wrapperType = typeof(QueryHandlerWrapperImpl<,>).MakeGenericType(tQuery, tResult);
                return Activator.CreateInstance(wrapperType)!;
            },
            handlerType);
    }

    /// <summary>
    /// Gets a cached stream query wrapper or creates one by resolving the handler type via PolymorphicHandlerResolver.
    /// Uses TryGetValue on hot path (single dict lookup), PolymorphicHandlerResolver only on cold path.
    /// </summary>
    private StreamQueryHandlerWrapper<TResult> GetOrCreateStreamWrapper<TResult>(Type queryType)
    {
        if (s_streamWrapperCache.TryGetValue(queryType, out object? cachedWrapper))
        {
            return (StreamQueryHandlerWrapper<TResult>)cachedWrapper;
        }

        Type handlerType = PolymorphicHandlerResolver.Resolve(
            queryType, typeof(IStreamQueryHandler<,>), _serviceProvider)
            ?? throw new NexumHandlerNotFoundException(queryType, "IStreamQueryHandler");

        return (StreamQueryHandlerWrapper<TResult>)s_streamWrapperCache.GetOrAdd(
            queryType,
            static (_, ht) =>
            {
                Type[] genericArgs = ht.GetGenericArguments();
                Type tQuery = genericArgs[0];
                Type tResult = genericArgs[1];
                Type wrapperType = typeof(StreamQueryHandlerWrapperImpl<,>).MakeGenericType(tQuery, tResult);
                return Activator.CreateInstance(wrapperType)!;
            },
            handlerType);
    }

    /// <summary>
    /// Gets the compiled query pipeline MethodInfo from NexumPipelineRegistry for the given query type.
    /// </summary>
    /// <param name="queryType">The concrete query type.</param>
    /// <returns>The MethodInfo for the compiled dispatch method, or null if not compiled.</returns>
    private MethodInfo? GetCompiledQueryMethod(Type queryType)
    {
        if (_pipelineRegistryType is null)
        {
            return null;
        }

        return s_compiledQueryMethodCache.GetOrAdd(queryType, key =>
        {
            // Get method name from NexumPipelineRegistry.GetQueryMethodName
            MethodInfo? getMethodName = _pipelineRegistryType.GetMethod("GetQueryMethodName",
                BindingFlags.Public | BindingFlags.Static);
            if (getMethodName is null)
            {
                return null;
            }

            string? methodName = (string?)getMethodName.Invoke(null, [key]);
            if (methodName is null)
            {
                return null;
            }

            // Get the actual dispatch method
            return _pipelineRegistryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        });
    }

    /// <summary>
    /// Gets the compiled stream query pipeline MethodInfo from NexumPipelineRegistry for the given query type.
    /// </summary>
    /// <param name="queryType">The concrete stream query type.</param>
    /// <returns>The MethodInfo for the compiled stream method, or null if not compiled.</returns>
    private MethodInfo? GetCompiledStreamQueryMethod(Type queryType)
    {
        if (_pipelineRegistryType is null)
        {
            return null;
        }

        return s_compiledStreamMethodCache.GetOrAdd(queryType, key =>
        {
            // Get method name from NexumPipelineRegistry.GetStreamQueryMethodName
            MethodInfo? getMethodName = _pipelineRegistryType.GetMethod("GetStreamQueryMethodName",
                BindingFlags.Public | BindingFlags.Static);
            if (getMethodName is null)
            {
                return null;
            }

            string? methodName = (string?)getMethodName.Invoke(null, [key]);
            if (methodName is null)
            {
                return null;
            }

            // Get the actual stream method
            return _pipelineRegistryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        });
    }

    /// <summary>
    /// Checks if any compiled behaviors have DI overrides in NexumOptions.BehaviorOrderOverrides.
    /// This implements the ADR-006 guard: if overrides are detected, fall back to runtime pipeline.
    /// </summary>
    /// <returns>True if any compiled behavior has a DI override; otherwise false.</returns>
    private bool CheckCompiledBehaviorOverrides()
    {
        if (_pipelineRegistryType is null || _options.BehaviorOrderOverrides.Count == 0)
        {
            return false;
        }

        // Get compiled behavior types from registry
        MethodInfo? getTypes = _pipelineRegistryType.GetMethod("GetCompiledBehaviorTypes",
            BindingFlags.Public | BindingFlags.Static);
        if (getTypes is null)
        {
            return false;
        }

        Type[]? compiledTypes = (Type[]?)getTypes.Invoke(null, null);
        if (compiledTypes is null || compiledTypes.Length == 0)
        {
            return false;
        }

        // Check if any compiled behavior type has a DI override
        foreach (Type compiledType in compiledTypes)
        {
            Type key = compiledType.IsGenericType ? compiledType.GetGenericTypeDefinition() : compiledType;
            if (_options.BehaviorOrderOverrides.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedQueryAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_options.MaxDispatchDepth < int.MaxValue)
        {
            return DispatchInterceptedQueryWithDepthGuardAsync(query, compiledPipeline, ct);
        }

        return DispatchInterceptedQueryFastAsync(query, compiledPipeline, ct);
    }

    /// <inheritdoc />
    IAsyncEnumerable<TResult> IInterceptableDispatcher.StreamInterceptedAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Step 1: Validate dispatch depth (synchronous setup)
        DispatchDepthGuard.DepthGuardScope depthGuardScope = default;
        bool depthGuardActive = _options.MaxDispatchDepth < int.MaxValue;
        try
        {
            if (depthGuardActive)
            {
                depthGuardScope = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);
            }

            return compiledPipeline(query, _serviceProvider, ct);
        }
        finally
        {
            if (depthGuardActive)
            {
                depthGuardScope.Dispose();
            }
        }
    }

    private ValueTask<TResult> DispatchInterceptedQueryFastAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TQuery : IQuery<TResult>
    {
        try
        {
            ValueTask<TResult> task = compiledPipeline(query, _serviceProvider, ct);
            if (task.IsCompletedSuccessfully)
            {
                return task; // async elision
            }

            return AwaitInterceptedQueryWithExceptionHandlingAsync(query, task, ct);
        }
        catch (Exception ex)
        {
            return HandleInterceptedQuerySyncExceptionAsync<TQuery, TResult>(query, ex, ct);
        }
    }

    private async ValueTask<TResult> DispatchInterceptedQueryWithDepthGuardAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TQuery : IQuery<TResult>
    {
        using DispatchDepthGuard.DepthGuardScope depthGuard = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);

        try
        {
            return await compiledPipeline(query, _serviceProvider, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    private async ValueTask<TResult> AwaitInterceptedQueryWithExceptionHandlingAsync<TQuery, TResult>(
        TQuery query,
        ValueTask<TResult> task,
        CancellationToken ct)
        where TQuery : IQuery<TResult>
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeQueryExceptionHandlersAsync(query, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    private async ValueTask<TResult> HandleInterceptedQuerySyncExceptionAsync<TQuery, TResult>(
        TQuery query,
        Exception ex,
        CancellationToken ct)
        where TQuery : IQuery<TResult>
    {
        await _exceptionHandlerResolver
            .InvokeQueryExceptionHandlersAsync(query, ex, ct)
            .ConfigureAwait(false);
        ExceptionDispatchInfo.Throw(ex);
        return default!; // Unreachable
    }

    /// <inheritdoc />
    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedCommandAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        throw new NotSupportedException("QueryDispatcher does not handle commands. Use CommandDispatcher instead.");
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ResetForTesting()
    {
        s_queryWrapperCache.Clear();
        s_streamWrapperCache.Clear();
        s_compiledQueryMethodCache.Clear();
        s_compiledStreamMethodCache.Clear();
        s_tier2QueryDelegateCache.Clear();
        s_hasRuntimeQueryBehaviorsCache.Clear();
    }
}
