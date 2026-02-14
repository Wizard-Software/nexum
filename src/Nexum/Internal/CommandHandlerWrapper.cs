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

internal sealed class CommandHandlerWrapperImpl<TCommand, TResult> : CommandHandlerWrapper<TResult>
    where TCommand : ICommand<TResult>
{
    private Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>>? _compiledDispatch;

    public override ValueTask<TResult> HandleAsync(
        ICommand<TResult> command,
        IServiceProvider serviceProvider,
        NexumOptions options,
        CancellationToken ct)
    {
        var typedCommand = (TCommand)command;
        var handler = (ICommandHandler<TCommand, TResult>)serviceProvider.GetRequiredService(
            typeof(ICommandHandler<TCommand, TResult>));

        // Fast path: no behaviors registered — direct handler call, zero allocations, zero async SM
        object? behaviorsObj = serviceProvider.GetService(
            typeof(IEnumerable<ICommandBehavior<TCommand, TResult>>));
        if (behaviorsObj is null or ICommandBehavior<TCommand, TResult>[] { Length: 0 })
        {
            return handler.HandleAsync(typedCommand, ct);
        }

        // Slow path: pass already-resolved behaviors to PipelineBuilder (no second DI lookup)
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
