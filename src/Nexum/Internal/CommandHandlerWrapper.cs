using System.Collections.Concurrent;
using System.Reflection;
using Nexum.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Internal;

internal abstract class CommandHandlerWrapper<TResult>
{
    public abstract ValueTask<TResult> HandleAsync(
        ICommand<TResult> command,
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
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken ct);

    /// <summary>
    /// Whether a compiled delegate has been cached for this wrapper.
    /// </summary>
    public bool HasCompiledDelegate { get; protected set; }
}

/// <summary>
/// Non-generic holder for the command behaviors cache — shared across all
/// <see cref="CommandHandlerWrapperImpl{TCommand,TResult}"/> instantiations.
/// Kept separate to allow <see cref="ResetForTesting"/> without type parameters.
/// </summary>
internal static class CommandHandlerWrapperCache
{
    /// <summary>
    /// Cache indicating whether any behaviors are registered per closed command type.
    /// Key: closed generic type key (TCommand + TResult encoded as the wrapper type).
    /// Value: true if behaviors exist, false otherwise.
    /// Null-miss means cold path — check and populate.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, bool> HasBehaviors = new();

    /// <summary>
    /// Cache of pipeline factories per (closed command type, NexumOptions instance) pair.
    /// Key: <c>(wrapper type, NexumOptions reference)</c> — the NexumOptions reference ensures that
    ///      different DI containers (e.g. in tests) produce separate cache entries and do not share
    ///      factories that bake in different behavior order overrides.
    /// Value: an opaque object holding the typed factory delegate
    ///        (<c>Func&lt;TCommand, ICommandHandler&lt;TCommand,TResult&gt;, IServiceProvider, CancellationToken, ValueTask&lt;TResult&gt;&gt;</c>).
    /// Populated on the cold path (first dispatch with behaviors).
    /// On subsequent dispatches the factory is called directly — no re-sorting.
    /// </summary>
    internal static readonly ConcurrentDictionary<(Type WrapperType, NexumOptions Options), object> PipelineFactories = new();

    /// <summary>Clears all caches. For testing purposes only.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetForTesting()
    {
        HasBehaviors.Clear();
        PipelineFactories.Clear();
    }
}

internal sealed class CommandHandlerWrapperImpl<TCommand, TResult> : CommandHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Stable type key for this generic instantiation — used as key in
    /// <see cref="CommandHandlerWrapperCache.HasBehaviors"/> and
    /// <see cref="CommandHandlerWrapperCache.PipelineFactories"/>.
    /// Using <c>typeof(CommandHandlerWrapperImpl&lt;TCommand, TResult&gt;)</c> as key uniquely
    /// identifies the (TCommand, TResult) pair without boxing.
    /// </summary>
    private static readonly Type s_cacheKey = typeof(CommandHandlerWrapperImpl<TCommand, TResult>);

    private Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>>? _compiledDispatch;

    public override ValueTask<TResult> HandleAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedCommand = (TCommand)command;

        // Zero-alloc hot path: skip DI lookup entirely when cached as "no behaviors"
        if (CommandHandlerWrapperCache.HasBehaviors.TryGetValue(s_cacheKey, out bool hasBehaviors) && !hasBehaviors)
        {
            var handler = (ICommandHandler<TCommand, TResult>)serviceProvider.GetRequiredService(
                typeof(ICommandHandler<TCommand, TResult>));
            return handler.HandleAsync(typedCommand, ct);
        }

        // Pipeline-factory hot path: behaviors exist and factory is cached — skip sorting.
        // Key includes NexumOptions instance so different DI containers (e.g. in tests) use
        // separate factories with their own BehaviorOrderOverrides baked in.
        var factoryKey = (s_cacheKey, options);
        if (hasBehaviors
            && CommandHandlerWrapperCache.PipelineFactories.TryGetValue(factoryKey, out object? factoryObj)
            && factoryObj is Func<TCommand, ICommandHandler<TCommand, TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>> factory)
        {
            var handler = (ICommandHandler<TCommand, TResult>)serviceProvider.GetRequiredService(
                typeof(ICommandHandler<TCommand, TResult>));
            return factory(typedCommand, handler, serviceProvider, ct);
        }

        return HandleWithBehaviorCheckAsync(typedCommand, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
    /// On first dispatch with behaviors, builds a pipeline factory capturing the sorted order and
    /// stores it in <see cref="CommandHandlerWrapperCache.PipelineFactories"/> for subsequent dispatches.
    /// Separated from the hot path to keep the inlineable fast path small.
    /// </summary>
    private ValueTask<TResult> HandleWithBehaviorCheckAsync(
        TCommand typedCommand,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var handler = (ICommandHandler<TCommand, TResult>)serviceProvider.GetRequiredService(
            typeof(ICommandHandler<TCommand, TResult>));

        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<ICommandBehavior<TCommand, TResult>>));

        if (behaviorsObj is null or ICommandBehavior<TCommand, TResult>[] { Length: 0 })
        {
            // No behaviors — cache and return direct handler call
            CommandHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, false);
            return handler.HandleAsync(typedCommand, ct);
        }

        // Behaviors exist — cache has-behaviors flag
        CommandHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, true);
        var behaviors = (ICommandBehavior<TCommand, TResult>[])behaviorsObj;

        // Build pipeline for this dispatch AND capture the sorted order for factory creation.
        // The factory is stored in PipelineFactories so subsequent dispatches skip sorting.
        CommandHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildCommandPipelineAndCaptureSorted(
            typedCommand, handler, behaviors, options, out ICommandBehavior<TCommand, TResult>[] sortedBehaviors);

        // Store factory only if not already present (another thread may have raced us here).
        // Key includes options reference — different DI containers use separate factory entries.
        CommandHandlerWrapperCache.PipelineFactories.TryAdd(
            (s_cacheKey, options),
            PipelineBuilder.BuildCommandPipelineFactory<TCommand, TResult>(sortedBehaviors));

        return pipeline(ct);
    }

    public override void SetCompiledDelegate(MethodInfo method)
    {
        _compiledDispatch = method.CreateDelegate<
            Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>>>();
        HasCompiledDelegate = true;
    }

    public override ValueTask<TResult> InvokeCompiledAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        CancellationToken ct)
    {
        return _compiledDispatch!((TCommand)command, serviceProvider, ct);
    }
}
