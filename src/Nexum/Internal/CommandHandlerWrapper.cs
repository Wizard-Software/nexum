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

    /// <summary>Clears the cache. For testing purposes only.</summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetForTesting() => HasBehaviors.Clear();
}

internal sealed class CommandHandlerWrapperImpl<TCommand, TResult> : CommandHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Stable type key for this generic instantiation — used as key in <see cref="CommandHandlerWrapperCache.HasBehaviors"/>.
    /// Using <c>typeof(CommandHandlerWrapperImpl&lt;TCommand, TResult&gt;)</c> as key uniquely identifies
    /// the (TCommand, TResult) pair without boxing.
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

        return HandleWithBehaviorCheckAsync(typedCommand, serviceProvider, options, ct);
    }

    /// <summary>
    /// Cold path: checks for behaviors (caches the result), then routes to direct call or pipeline.
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

        // Behaviors exist — cache and build pipeline
        CommandHandlerWrapperCache.HasBehaviors.TryAdd(s_cacheKey, true);
        var behaviors = (IEnumerable<ICommandBehavior<TCommand, TResult>>)behaviorsObj;
        CommandHandlerDelegate<TResult> pipeline = PipelineBuilder.BuildCommandPipeline(
            typedCommand, handler, behaviors, options);
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
