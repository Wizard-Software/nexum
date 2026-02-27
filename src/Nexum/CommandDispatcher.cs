using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum;

/// <summary>
/// Default implementation of <see cref="ICommandDispatcher"/> that dispatches commands to their handlers
/// through a behavior pipeline with exception handling support.
/// </summary>
/// <remarks>
/// <para>
/// This dispatcher is thread-safe and designed to be registered as a singleton.
/// It uses a cached handler wrapper for the runtime path (without Source Generator)
/// that eliminates per-dispatch reflection overhead.
/// </para>
/// <para>
/// The dispatch flow:
/// </para>
/// <list type="number">
/// <item>Validate dispatch depth to prevent stack overflow in recursive scenarios</item>
/// <item>Resolve the handler type polymorphically (supports base class handlers)</item>
/// <item>Obtain a cached wrapper and build the behavior pipeline (Russian doll pattern)</item>
/// <item>Execute the pipeline and invoke exception handlers on failure (always re-throws)</item>
/// </list>
/// </remarks>
public sealed class CommandDispatcher : ICommandDispatcher, IInterceptableDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NexumOptions _options;
    private readonly ExceptionHandlerResolver _exceptionHandlerResolver;
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Cache of handler wrappers for the runtime path.
    /// </summary>
    /// <remarks>
    /// Key: commandType (runtime type of the command)
    /// Value: <see cref="CommandHandlerWrapper{TResult}"/> (stored as object because TResult varies)
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Wrapper creation is idempotent and stateless — benign race on first access is harmless.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, object> s_wrapperCache = new();

    /// <summary>
    /// Cache of compiled pipeline MethodInfo from NexumPipelineRegistry (Tier 2).
    /// </summary>
    /// <remarks>
    /// Key: commandType
    /// Value: MethodInfo for the compiled dispatch method, or null if not compiled
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, MethodInfo?> s_compiledMethodCache = new();

    /// <summary>
    /// Cache of Tier 2 dispatch delegates per command type.
    /// Stores typed Func delegates that invoke compiled pipeline directly (zero lambda closure per dispatch).
    /// </summary>
    /// <remarks>
    /// Key: commandType
    /// Value: Func&lt;ICommand&lt;TResult&gt;, IServiceProvider, CancellationToken, ValueTask&lt;TResult&gt;&gt; (stored as object because TResult varies)
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Only populated when compiled pipeline exists AND no runtime behaviors are registered.
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, object> s_tier2DelegateCache = new();

    /// <summary>
    /// Cache indicating whether a command type has any runtime (non-compiled) behaviors registered.
    /// When false, the Tier 2 fast path can skip WrapCommandWithRuntimeBehaviors entirely.
    /// DI container is immutable after build — this cache is safe for the application lifetime.
    /// </summary>
    /// <remarks>
    /// Key: commandType
    /// Value: true if runtime behaviors exist, false otherwise
    /// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>.
    /// Cached on first dispatch per command type (benign race).
    /// </remarks>
    private static readonly ConcurrentDictionary<Type, bool> s_hasRuntimeBehaviorsCache = new();

    // Tier 2: Set in constructor from NexumOptions
    private readonly Type? _pipelineRegistryType;
    private readonly Func<Type, bool>? _isCompiledBehavior;
    private readonly Lazy<bool> _hasCompiledBehaviorOverrides;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandDispatcher"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving handlers and behaviors.</param>
    /// <param name="options">The Nexum configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> or <paramref name="options"/> is null.</exception>
    public CommandDispatcher(IServiceProvider serviceProvider, NexumOptions options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _exceptionHandlerResolver = serviceProvider.GetRequiredService<ExceptionHandlerResolver>();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

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
        ICommand<TResult> command,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_options.MaxDispatchDepth < int.MaxValue)
        {
            return DispatchWithDepthGuardAsync(command, ct);
        }

        if (_pipelineRegistryType is not null)
        {
            return DispatchTier2FastAsync(command, ct);
        }

        return DispatchRuntimeFastAsync(command, ct);
    }

    /// <summary>
    /// Full async path with depth guard scope (default configuration: MaxDispatchDepth = 16).
    /// Handles both Tier 2 (compiled pipeline) and Runtime (wrapper) paths.
    /// </summary>
    private async ValueTask<TResult> DispatchWithDepthGuardAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken ct)
    {
        using DispatchDepthGuard.DepthGuardScope depthGuard = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);

        Type commandType = command.GetType();

        try
        {
            // Tier 2: Try compiled pipeline first
            if (_pipelineRegistryType is not null
                && _isCompiledBehavior is not null
                && !_hasCompiledBehaviorOverrides.Value)
            {
                MethodInfo? compiledMethod = GetCompiledCommandMethod(commandType);
                if (compiledMethod is not null)
                {
                    // Use wrapper's cached delegate (zero reflection after cold path)
                    CommandHandlerWrapper<TResult> compiledWrapper = GetOrCreateWrapper<TResult>(commandType);
                    if (!compiledWrapper.HasCompiledDelegate)
                    {
                        compiledWrapper.SetCompiledDelegate(compiledMethod);
                    }

                    // Check if runtime behaviors exist (cached per command type)
                    if (s_hasRuntimeBehaviorsCache.TryGetValue(commandType, out bool hasRtBehaviors) && !hasRtBehaviors)
                    {
                        // No runtime behaviors — invoke compiled delegate directly
                        return await compiledWrapper.InvokeCompiledAsync(command, _serviceProvider, ct)
                            .ConfigureAwait(false);
                    }

                    // Has runtime behaviors OR first dispatch (not cached yet) — use WrapCommand
                    CommandHandlerDelegate<TResult> compiledPipeline =
                        innerCt => compiledWrapper.InvokeCompiledAsync(command, _serviceProvider, innerCt);
                    CommandHandlerDelegate<TResult> fullPipeline = PipelineBuilder.WrapCommandWithRuntimeBehaviors(
                        _serviceProvider, command, compiledPipeline, _options, _isCompiledBehavior);

                    // Cache the runtime behaviors flag on first dispatch
                    s_hasRuntimeBehaviorsCache.TryAdd(commandType, !ReferenceEquals(compiledPipeline, fullPipeline));

                    return await fullPipeline(ct).ConfigureAwait(false);
                }
            }

            // Runtime path — TryGetValue fast path (skip PolymorphicHandlerResolver on steady-state)
            CommandHandlerWrapper<TResult> wrapper = GetOrCreateWrapper<TResult>(commandType);
            return await wrapper.HandleAsync(command, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Non-async fast path for Tier 2 (SG compiled pipeline) without depth guard.
    /// Avoids async state machine allocation on synchronously completing handlers.
    /// </summary>
    private ValueTask<TResult> DispatchTier2FastAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken ct)
    {
        Type commandType = command.GetType();

        // Hot path: try cached Tier 2 delegate first
        if (s_tier2DelegateCache.TryGetValue(commandType, out object? cached))
        {
            var del = (Func<ICommand<TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>)cached;
            try
            {
                ValueTask<TResult> task = del(command, _serviceProvider, ct);
                if (task.IsCompletedSuccessfully)
                {
                    return task;
                }

                return AwaitWithExceptionHandlingAsync(command, task, ct);
            }
            catch (Exception ex)
            {
                return HandleSyncExceptionAsync<TResult>(command, ex, ct);
            }
        }

        // Cold path: setup and cache
        return DispatchTier2ColdAsync(command, commandType, ct);
    }

    /// <summary>
    /// Cold path for Tier 2 dispatch: resolves compiled method, caches wrapper and delegate if no runtime behaviors.
    /// Falls back to runtime wrapper if no compiled method is found.
    /// </summary>
    private async ValueTask<TResult> DispatchTier2ColdAsync<TResult>(
        ICommand<TResult> command,
        Type commandType,
        CancellationToken ct)
    {
        try
        {
            if (_isCompiledBehavior is not null
                && !_hasCompiledBehaviorOverrides.Value)
            {
                MethodInfo? compiledMethod = GetCompiledCommandMethod(commandType);
                if (compiledMethod is not null)
                {
                    // Setup wrapper's cached delegate (zero reflection after cold path)
                    CommandHandlerWrapper<TResult> compiledWrapper = GetOrCreateWrapper<TResult>(commandType);
                    if (!compiledWrapper.HasCompiledDelegate)
                    {
                        compiledWrapper.SetCompiledDelegate(compiledMethod);
                    }

                    CommandHandlerDelegate<TResult> compiledPipeline =
                        innerCt => compiledWrapper.InvokeCompiledAsync(command, _serviceProvider, innerCt);
                    CommandHandlerDelegate<TResult> fullPipeline = PipelineBuilder.WrapCommandWithRuntimeBehaviors(
                        _serviceProvider, command, compiledPipeline, _options, _isCompiledBehavior);

                    // Check if runtime behaviors exist (identity check: compiledPipeline == fullPipeline means no runtime behaviors)
                    bool hasRuntimeBehaviors = !ReferenceEquals(compiledPipeline, fullPipeline);
                    s_hasRuntimeBehaviorsCache.TryAdd(commandType, hasRuntimeBehaviors);

                    if (!hasRuntimeBehaviors)
                    {
                        // Fast path: no runtime behaviors — cache delegate for future hot-path use
                        CommandHandlerWrapper<TResult> cachedWrapper = compiledWrapper;
                        s_tier2DelegateCache.TryAdd(commandType,
                            (Func<ICommand<TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>)
                            ((cmd, sp, token) => cachedWrapper.InvokeCompiledAsync(cmd, sp, token)));
                    }

                    return await fullPipeline(ct).ConfigureAwait(false);
                }
            }

            // Fallback to runtime wrapper
            CommandHandlerWrapper<TResult> wrapper = GetOrCreateWrapper<TResult>(commandType);
            return await wrapper.HandleAsync(command, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Non-async fast path for runtime dispatch (no depth guard, no Tier 2).
    /// Avoids async state machine allocation on synchronously completing handlers.
    /// </summary>
    private ValueTask<TResult> DispatchRuntimeFastAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken ct)
    {
        Type commandType = command.GetType();

        // Hot path: cached wrapper lookup (single ConcurrentDict read)
        if (!s_wrapperCache.TryGetValue(commandType, out object? cachedWrapper))
        {
            return DispatchRuntimeColdAsync(command, commandType, ct);
        }

        var wrapper = (CommandHandlerWrapper<TResult>)cachedWrapper;

        try
        {
            ValueTask<TResult> task = wrapper.HandleAsync(command, _serviceProvider, _options, ct);
            if (task.IsCompletedSuccessfully)
            {
                return task;
            }

            return AwaitWithExceptionHandlingAsync(command, task, ct);
        }
        catch (Exception ex)
        {
            return HandleSyncExceptionAsync<TResult>(command, ex, ct);
        }
    }

    /// <summary>
    /// Cold path for runtime dispatch: resolves handler type, caches wrapper, then dispatches.
    /// </summary>
    private async ValueTask<TResult> DispatchRuntimeColdAsync<TResult>(
        ICommand<TResult> command,
        Type commandType,
        CancellationToken ct)
    {
        try
        {
            CommandHandlerWrapper<TResult> wrapper = GetOrCreateWrapper<TResult>(commandType);
            return await wrapper.HandleAsync(command, _serviceProvider, _options, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Awaits an asynchronously completing handler and wraps with exception handler invocation.
    /// </summary>
    private async ValueTask<TResult> AwaitWithExceptionHandlingAsync<TResult>(
        ICommand<TResult> command,
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
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
    }

    /// <summary>
    /// Handles a synchronous exception from non-async dispatch path by invoking exception handlers
    /// and re-throwing with preserved stack trace.
    /// </summary>
    private async ValueTask<TResult> HandleSyncExceptionAsync<TResult>(
        ICommand<TResult> command,
        Exception ex,
        CancellationToken ct)
    {
        await _exceptionHandlerResolver
            .InvokeCommandExceptionHandlersAsync(command, ex, ct)
            .ConfigureAwait(false);
        ExceptionDispatchInfo.Throw(ex); // Preserves original stack trace
        return default!; // Unreachable — ExceptionDispatchInfo.Throw always throws
    }

    /// <summary>
    /// Gets a cached wrapper or creates one by resolving the handler type via PolymorphicHandlerResolver.
    /// Uses TryGetValue on hot path (single dict lookup), PolymorphicHandlerResolver only on cold path.
    /// </summary>
    private CommandHandlerWrapper<TResult> GetOrCreateWrapper<TResult>(Type commandType)
    {
        if (s_wrapperCache.TryGetValue(commandType, out object? cachedWrapper))
        {
            return (CommandHandlerWrapper<TResult>)cachedWrapper;
        }

        Type handlerType = PolymorphicHandlerResolver.Resolve(
            commandType, typeof(ICommandHandler<,>), _serviceProvider)
            ?? throw new NexumHandlerNotFoundException(commandType, "ICommandHandler");

        return (CommandHandlerWrapper<TResult>)s_wrapperCache.GetOrAdd(
            commandType,
            static (_, ht) =>
            {
                Type[] genericArgs = ht.GetGenericArguments();
                Type tCommand = genericArgs[0];
                Type tResult = genericArgs[1];
                Type wrapperType = typeof(CommandHandlerWrapperImpl<,>).MakeGenericType(tCommand, tResult);
                return Activator.CreateInstance(wrapperType)!;
            },
            handlerType);
    }

    /// <summary>
    /// Gets the compiled pipeline MethodInfo from NexumPipelineRegistry for the given command type.
    /// </summary>
    /// <param name="commandType">The concrete command type.</param>
    /// <returns>The MethodInfo for the compiled dispatch method, or null if not compiled.</returns>
    private MethodInfo? GetCompiledCommandMethod(Type commandType)
    {
        if (_pipelineRegistryType is null)
        {
            return null;
        }

        return s_compiledMethodCache.GetOrAdd(commandType, key =>
        {
            // Get method name from NexumPipelineRegistry.GetCommandMethodName
            MethodInfo? getMethodName = _pipelineRegistryType.GetMethod("GetCommandMethodName",
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
    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedCommandAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_options.MaxDispatchDepth < int.MaxValue)
        {
            return DispatchInterceptedCommandWithDepthGuardAsync(command, compiledPipeline, ct);
        }

        return DispatchInterceptedCommandFastAsync(command, compiledPipeline, ct);
    }

    private ValueTask<TResult> DispatchInterceptedCommandFastAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TCommand : ICommand<TResult>
    {
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        try
        {
            ValueTask<TResult> task = compiledPipeline(command, scope.ServiceProvider, ct);
            if (task.IsCompletedSuccessfully)
            {
                scope.Dispose();
                return task; // async elision
            }

            return AwaitInterceptedCommandWithScopeAsync(command, scope, task, ct);
        }
        catch (Exception ex)
        {
            scope.Dispose();
            return HandleInterceptedCommandSyncExceptionAsync<TCommand, TResult>(command, ex, ct);
        }
    }

    private async ValueTask<TResult> DispatchInterceptedCommandWithDepthGuardAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
        where TCommand : ICommand<TResult>
    {
        using DispatchDepthGuard.DepthGuardScope depthGuard = DispatchDepthGuard.Enter(_options.MaxDispatchDepth);
        AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();

        try
        {
            return await compiledPipeline(command, scope.ServiceProvider, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<TResult> AwaitInterceptedCommandWithScopeAsync<TCommand, TResult>(
        TCommand command,
        AsyncServiceScope scope,
        ValueTask<TResult> task,
        CancellationToken ct)
        where TCommand : ICommand<TResult>
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await _exceptionHandlerResolver
                .InvokeCommandExceptionHandlersAsync(command, ex, ct)
                .ConfigureAwait(false);
            throw; // ALWAYS re-throw (Z5)
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async ValueTask<TResult> HandleInterceptedCommandSyncExceptionAsync<TCommand, TResult>(
        TCommand command,
        Exception ex,
        CancellationToken ct)
        where TCommand : ICommand<TResult>
    {
        await _exceptionHandlerResolver
            .InvokeCommandExceptionHandlersAsync(command, ex, ct)
            .ConfigureAwait(false);
        ExceptionDispatchInfo.Throw(ex);
        return default!; // Unreachable
    }

    /// <inheritdoc />
    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedQueryAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        throw new NotSupportedException("CommandDispatcher does not handle queries. Use QueryDispatcher instead.");
    }

    /// <inheritdoc />
    IAsyncEnumerable<TResult> IInterceptableDispatcher.StreamInterceptedAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        throw new NotSupportedException("CommandDispatcher does not handle stream queries. Use QueryDispatcher instead.");
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void ResetForTesting()
    {
        s_wrapperCache.Clear();
        s_compiledMethodCache.Clear();
        s_tier2DelegateCache.Clear();
        s_hasRuntimeBehaviorsCache.Clear();
    }
}
