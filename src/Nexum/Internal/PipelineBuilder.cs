using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Builds execution pipelines for commands and queries by chaining behaviors around handlers.
/// </summary>
/// <remarks>
/// Pipelines are built per-dispatch (not cached) since behaviors are Transient-scoped.
/// Behaviors are ordered by <see cref="BehaviorOrderAttribute"/> (ascending), then by insertion order.
/// </remarks>
internal static class PipelineBuilder
{
    /// <summary>
    /// Cache for <see cref="BehaviorOrderAttribute"/> lookups per behavior type.
    /// Only caches attribute-based order (not overrides from <see cref="NexumOptions"/>).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, int> s_behaviorOrderCache = new();

    /// <summary>
    /// Builds a command execution pipeline by wrapping the handler with resolved behaviors.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="command">The command instance to be handled.</param>
    /// <param name="handler">The resolved command handler (passed by dispatcher).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static CommandHandlerDelegate<TResult> BuildCommandPipeline<TCommand, TResult>(
        IServiceProvider sp,
        TCommand command,
        ICommandHandler<TCommand, TResult> handler,
        NexumOptions options)
        where TCommand : ICommand<TResult>
    {
        // Resolve all registered behaviors for this command type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<ICommandBehavior<TCommand, TResult>>));

        // If no behaviors registered, return handler-only pipeline
        if (behaviorsObj is not IEnumerable<ICommandBehavior<TCommand, TResult>> behaviors)
        {
            return ct => handler.HandleAsync(command, ct);
        }

        return BuildCommandPipeline(command, handler, behaviors, options);
    }

    /// <summary>
    /// Builds a command execution pipeline using pre-resolved behaviors (avoids duplicate DI resolution).
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="command">The command instance to be handled.</param>
    /// <param name="handler">The resolved command handler (passed by dispatcher).</param>
    /// <param name="behaviors">Pre-resolved behaviors for this command type.</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static CommandHandlerDelegate<TResult> BuildCommandPipeline<TCommand, TResult>(
        TCommand command,
        ICommandHandler<TCommand, TResult> handler,
        IEnumerable<ICommandBehavior<TCommand, TResult>> behaviors,
        NexumOptions options)
        where TCommand : ICommand<TResult>
    {
        // Fast path: when DI returns T[] (MS DI always does) and no ordering is needed,
        // build delegate chain directly from array — no List allocation, no Sort
        if (behaviors is ICommandBehavior<TCommand, TResult>[] arr)
        {
            if (arr.Length == 0)
            {
                return ct => handler.HandleAsync(command, ct);
            }

            if (options.BehaviorOrderOverrides.Count == 0 && AllDefaultOrder(arr))
            {
                CommandHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(command, ct);
                for (int i = arr.Length - 1; i >= 0; i--)
                {
                    ICommandBehavior<TCommand, TResult> behavior = arr[i];
                    CommandHandlerDelegate<TResult> next = pipeline;
                    pipeline = ct => behavior.HandleAsync(command, next, ct);
                }

                return pipeline;
            }
        }

        // Slow path: sort behaviors by [BehaviorOrder] attribute (ascending), then by insertion order
        var sortedBehaviors = new List<(ICommandBehavior<TCommand, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (var behavior in behaviors)
        {
            sortedBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no behaviors after enumeration, return handler-only pipeline
        if (sortedBehaviors.Count == 0)
        {
            return ct => handler.HandleAsync(command, ct);
        }

        sortedBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain from innermost (handler) to outermost (first behavior)
        CommandHandlerDelegate<TResult> pipeline2 = ct => handler.HandleAsync(command, ct);

        for (int i = sortedBehaviors.Count - 1; i >= 0; i--)
        {
            ICommandBehavior<TCommand, TResult> behavior = sortedBehaviors[i].Behavior;
            CommandHandlerDelegate<TResult> next = pipeline2; // Capture current delegate in closure
            pipeline2 = ct => behavior.HandleAsync(command, next, ct);
        }

        return pipeline2;
    }

    /// <summary>
    /// Builds a query execution pipeline by wrapping the handler with resolved behaviors.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved query handler (passed by dispatcher).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static QueryHandlerDelegate<TResult> BuildQueryPipeline<TQuery, TResult>(
        IServiceProvider sp,
        TQuery query,
        IQueryHandler<TQuery, TResult> handler,
        NexumOptions options)
        where TQuery : IQuery<TResult>
    {
        // Resolve all registered behaviors for this query type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<IQueryBehavior<TQuery, TResult>>));

        // If no behaviors registered, return handler-only pipeline
        if (behaviorsObj is not IEnumerable<IQueryBehavior<TQuery, TResult>> behaviors)
        {
            return ct => handler.HandleAsync(query, ct);
        }

        return BuildQueryPipeline(query, handler, behaviors, options);
    }

    /// <summary>
    /// Builds a query execution pipeline using pre-resolved behaviors (avoids duplicate DI resolution).
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved query handler (passed by dispatcher).</param>
    /// <param name="behaviors">Pre-resolved behaviors for this query type.</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static QueryHandlerDelegate<TResult> BuildQueryPipeline<TQuery, TResult>(
        TQuery query,
        IQueryHandler<TQuery, TResult> handler,
        IEnumerable<IQueryBehavior<TQuery, TResult>> behaviors,
        NexumOptions options)
        where TQuery : IQuery<TResult>
    {
        // Fast path: when DI returns T[] (MS DI always does) and no ordering is needed,
        // build delegate chain directly from array — no List allocation, no Sort
        if (behaviors is IQueryBehavior<TQuery, TResult>[] arr)
        {
            if (arr.Length == 0)
            {
                return ct => handler.HandleAsync(query, ct);
            }

            if (options.BehaviorOrderOverrides.Count == 0 && AllDefaultOrder(arr))
            {
                QueryHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(query, ct);
                for (int i = arr.Length - 1; i >= 0; i--)
                {
                    IQueryBehavior<TQuery, TResult> behavior = arr[i];
                    QueryHandlerDelegate<TResult> next = pipeline;
                    pipeline = ct => behavior.HandleAsync(query, next, ct);
                }

                return pipeline;
            }
        }

        // Slow path: sort behaviors by [BehaviorOrder] attribute (ascending), then by insertion order
        var sortedBehaviors = new List<(IQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (var behavior in behaviors)
        {
            sortedBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no behaviors after enumeration, return handler-only pipeline
        if (sortedBehaviors.Count == 0)
        {
            return ct => handler.HandleAsync(query, ct);
        }

        sortedBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain from innermost (handler) to outermost (first behavior)
        QueryHandlerDelegate<TResult> pipeline2 = ct => handler.HandleAsync(query, ct);

        for (int i = sortedBehaviors.Count - 1; i >= 0; i--)
        {
            IQueryBehavior<TQuery, TResult> behavior = sortedBehaviors[i].Behavior;
            QueryHandlerDelegate<TResult> next = pipeline2; // Capture current delegate in closure
            pipeline2 = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline2;
    }

    /// <summary>
    /// Builds a streaming query execution pipeline by wrapping the handler with resolved behaviors.
    /// </summary>
    /// <typeparam name="TQuery">The streaming query type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved streaming query handler (passed by dispatcher).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static StreamQueryHandlerDelegate<TResult> BuildStreamQueryPipeline<TQuery, TResult>(
        IServiceProvider sp,
        TQuery query,
        IStreamQueryHandler<TQuery, TResult> handler,
        NexumOptions options)
        where TQuery : IStreamQuery<TResult>
    {
        // Resolve all registered behaviors for this streaming query type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<IStreamQueryBehavior<TQuery, TResult>>));

        // If no behaviors registered, return handler-only pipeline
        if (behaviorsObj is not IEnumerable<IStreamQueryBehavior<TQuery, TResult>> behaviors)
        {
            return ct => handler.HandleAsync(query, ct);
        }

        return BuildStreamQueryPipeline(query, handler, behaviors, options);
    }

    /// <summary>
    /// Builds a streaming query execution pipeline using pre-resolved behaviors (avoids duplicate DI resolution).
    /// </summary>
    /// <typeparam name="TQuery">The streaming query type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved streaming query handler (passed by dispatcher).</param>
    /// <param name="behaviors">Pre-resolved behaviors for this streaming query type.</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    /// <remarks>
    /// The pipeline is constructed from innermost (handler) to outermost (first behavior).
    /// If no behaviors are registered, the pipeline consists of the handler only.
    /// </remarks>
    internal static StreamQueryHandlerDelegate<TResult> BuildStreamQueryPipeline<TQuery, TResult>(
        TQuery query,
        IStreamQueryHandler<TQuery, TResult> handler,
        IEnumerable<IStreamQueryBehavior<TQuery, TResult>> behaviors,
        NexumOptions options)
        where TQuery : IStreamQuery<TResult>
    {
        // Fast path: when DI returns T[] (MS DI always does) and no ordering is needed,
        // build delegate chain directly from array — no List allocation, no Sort
        if (behaviors is IStreamQueryBehavior<TQuery, TResult>[] arr)
        {
            if (arr.Length == 0)
            {
                return ct => handler.HandleAsync(query, ct);
            }

            if (options.BehaviorOrderOverrides.Count == 0 && AllDefaultOrder(arr))
            {
                StreamQueryHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(query, ct);
                for (int i = arr.Length - 1; i >= 0; i--)
                {
                    IStreamQueryBehavior<TQuery, TResult> behavior = arr[i];
                    StreamQueryHandlerDelegate<TResult> next = pipeline;
                    pipeline = ct => behavior.HandleAsync(query, next, ct);
                }

                return pipeline;
            }
        }

        // Slow path: sort behaviors by [BehaviorOrder] attribute (ascending), then by insertion order
        var sortedBehaviors = new List<(IStreamQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (var behavior in behaviors)
        {
            sortedBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no behaviors after enumeration, return handler-only pipeline
        if (sortedBehaviors.Count == 0)
        {
            return ct => handler.HandleAsync(query, ct);
        }

        sortedBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain from innermost (handler) to outermost (first behavior)
        StreamQueryHandlerDelegate<TResult> pipeline2 = ct => handler.HandleAsync(query, ct);

        for (int i = sortedBehaviors.Count - 1; i >= 0; i--)
        {
            IStreamQueryBehavior<TQuery, TResult> behavior = sortedBehaviors[i].Behavior;
            StreamQueryHandlerDelegate<TResult> next = pipeline2; // Capture current delegate in closure
            pipeline2 = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline2;
    }

    /// <summary>
    /// Wraps a compiled command pipeline delegate with runtime-only behaviors.
    /// Filters out behaviors that are already baked into the compiled pipeline.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="command">The command instance to be handled.</param>
    /// <param name="compiledPipeline">The compiled pipeline delegate (with behaviors already baked in).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <param name="isCompiledBehavior">Predicate to check if a behavior type is already compiled.</param>
    /// <returns>A delegate representing the wrapped pipeline with runtime behaviors only.</returns>
    /// <remarks>
    /// Runtime behaviors (outermost) wrap the compiled pipeline (innermost).
    /// If no runtime behaviors are found, the compiled pipeline is returned as-is (zero overhead).
    /// </remarks>
    internal static CommandHandlerDelegate<TResult> WrapCommandWithRuntimeBehaviors<TCommand, TResult>(
        IServiceProvider sp,
        TCommand command,
        CommandHandlerDelegate<TResult> compiledPipeline,
        NexumOptions options,
        Func<Type, bool> isCompiledBehavior)
        where TCommand : ICommand<TResult>
    {
        // Resolve all registered behaviors for this command type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<ICommandBehavior<TCommand, TResult>>));

        // If no behaviors registered at all, return compiled pipeline as-is
        if (behaviorsObj is not IEnumerable<ICommandBehavior<TCommand, TResult>> behaviors)
        {
            return compiledPipeline;
        }

        // Filter out compiled behaviors, keep only runtime ones
        var runtimeBehaviors = new List<(ICommandBehavior<TCommand, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (ICommandBehavior<TCommand, TResult>? behavior in behaviors)
        {
            // Skip behaviors that are already in the compiled pipeline
            if (isCompiledBehavior(behavior.GetType()))
            {
                index++;
                continue;
            }

            runtimeBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no runtime behaviors, return compiled pipeline as-is
        if (runtimeBehaviors.Count == 0)
        {
            return compiledPipeline;
        }

        // Sort by order ascending, then by index
        runtimeBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain: runtime behaviors (outermost) → compiled pipeline (innermost)
        CommandHandlerDelegate<TResult> pipeline = compiledPipeline;

        for (int i = runtimeBehaviors.Count - 1; i >= 0; i--)
        {
            ICommandBehavior<TCommand, TResult> behavior = runtimeBehaviors[i].Behavior;
            CommandHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(command, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// Wraps a compiled query pipeline delegate with runtime-only behaviors.
    /// Filters out behaviors that are already baked into the compiled pipeline.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="compiledPipeline">The compiled pipeline delegate (with behaviors already baked in).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <param name="isCompiledBehavior">Predicate to check if a behavior type is already compiled.</param>
    /// <returns>A delegate representing the wrapped pipeline with runtime behaviors only.</returns>
    /// <remarks>
    /// Runtime behaviors (outermost) wrap the compiled pipeline (innermost).
    /// If no runtime behaviors are found, the compiled pipeline is returned as-is (zero overhead).
    /// </remarks>
    internal static QueryHandlerDelegate<TResult> WrapQueryWithRuntimeBehaviors<TQuery, TResult>(
        IServiceProvider sp,
        TQuery query,
        QueryHandlerDelegate<TResult> compiledPipeline,
        NexumOptions options,
        Func<Type, bool> isCompiledBehavior)
        where TQuery : IQuery<TResult>
    {
        // Resolve all registered behaviors for this query type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<IQueryBehavior<TQuery, TResult>>));

        // If no behaviors registered at all, return compiled pipeline as-is
        if (behaviorsObj is not IEnumerable<IQueryBehavior<TQuery, TResult>> behaviors)
        {
            return compiledPipeline;
        }

        // Filter out compiled behaviors, keep only runtime ones
        var runtimeBehaviors = new List<(IQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (IQueryBehavior<TQuery, TResult>? behavior in behaviors)
        {
            // Skip behaviors that are already in the compiled pipeline
            if (isCompiledBehavior(behavior.GetType()))
            {
                index++;
                continue;
            }

            runtimeBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no runtime behaviors, return compiled pipeline as-is
        if (runtimeBehaviors.Count == 0)
        {
            return compiledPipeline;
        }

        // Sort by order ascending, then by index
        runtimeBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain: runtime behaviors (outermost) → compiled pipeline (innermost)
        QueryHandlerDelegate<TResult> pipeline = compiledPipeline;

        for (int i = runtimeBehaviors.Count - 1; i >= 0; i--)
        {
            IQueryBehavior<TQuery, TResult> behavior = runtimeBehaviors[i].Behavior;
            QueryHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// Wraps a compiled streaming query pipeline delegate with runtime-only behaviors.
    /// Filters out behaviors that are already baked into the compiled pipeline.
    /// </summary>
    /// <typeparam name="TQuery">The streaming query type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="sp">The service provider for resolving behaviors.</param>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="compiledPipeline">The compiled pipeline delegate (with behaviors already baked in).</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <param name="isCompiledBehavior">Predicate to check if a behavior type is already compiled.</param>
    /// <returns>A delegate representing the wrapped pipeline with runtime behaviors only.</returns>
    /// <remarks>
    /// Runtime behaviors (outermost) wrap the compiled pipeline (innermost).
    /// If no runtime behaviors are found, the compiled pipeline is returned as-is (zero overhead).
    /// </remarks>
    internal static StreamQueryHandlerDelegate<TResult> WrapStreamQueryWithRuntimeBehaviors<TQuery, TResult>(
        IServiceProvider sp,
        TQuery query,
        StreamQueryHandlerDelegate<TResult> compiledPipeline,
        NexumOptions options,
        Func<Type, bool> isCompiledBehavior)
        where TQuery : IStreamQuery<TResult>
    {
        // Resolve all registered behaviors for this streaming query type
        object? behaviorsObj = sp.GetService(typeof(IEnumerable<IStreamQueryBehavior<TQuery, TResult>>));

        // If no behaviors registered at all, return compiled pipeline as-is
        if (behaviorsObj is not IEnumerable<IStreamQueryBehavior<TQuery, TResult>> behaviors)
        {
            return compiledPipeline;
        }

        // Filter out compiled behaviors, keep only runtime ones
        var runtimeBehaviors = new List<(IStreamQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>();
        int index = 0;
        foreach (IStreamQueryBehavior<TQuery, TResult>? behavior in behaviors)
        {
            // Skip behaviors that are already in the compiled pipeline
            if (isCompiledBehavior(behavior.GetType()))
            {
                index++;
                continue;
            }

            runtimeBehaviors.Add((behavior, GetBehaviorOrder(behavior, options), index));
            index++;
        }

        // If no runtime behaviors, return compiled pipeline as-is
        if (runtimeBehaviors.Count == 0)
        {
            return compiledPipeline;
        }

        // Sort by order ascending, then by index
        runtimeBehaviors.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Build delegate chain: runtime behaviors (outermost) → compiled pipeline (innermost)
        StreamQueryHandlerDelegate<TResult> pipeline = compiledPipeline;

        for (int i = runtimeBehaviors.Count - 1; i >= 0; i--)
        {
            IStreamQueryBehavior<TQuery, TResult> behavior = runtimeBehaviors[i].Behavior;
            StreamQueryHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// Returns true when every element in the array has a cached [BehaviorOrder] of 0
    /// (or no attribute at all). Used by the fast path to skip List + Sort.
    /// </summary>
    private static bool AllDefaultOrder(object[] behaviors)
    {
        for (int i = 0; i < behaviors.Length; i++)
        {
            int order = s_behaviorOrderCache.GetOrAdd(
                behaviors[i].GetType(),
                static t => t.GetCustomAttribute<BehaviorOrderAttribute>(inherit: false)?.Order ?? 0);
            if (order != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads the behavior order from <see cref="NexumOptions.BehaviorOrderOverrides"/> or the <see cref="BehaviorOrderAttribute"/>.
    /// </summary>
    /// <param name="behavior">The behavior instance.</param>
    /// <param name="options">Nexum runtime options containing behavior order overrides.</param>
    /// <returns>The order value from overrides, attribute, or 0 if neither is present.</returns>
    private static int GetBehaviorOrder(object behavior, NexumOptions options)
    {
        Type behaviorType = behavior.GetType();

        // Check overrides first (from AddNexumBehavior(order:))
        if (options.BehaviorOrderOverrides.TryGetValue(behaviorType, out int overrideOrder))
        {
            return overrideOrder;
        }

        // If the behavior is a closed generic, also check the open generic type definition
        // AddNexumBehavior(typeof(MyBehavior<,>), order: N) stores the open generic as key
        if (behaviorType.IsGenericType
            && options.BehaviorOrderOverrides.TryGetValue(behaviorType.GetGenericTypeDefinition(), out overrideOrder))
        {
            return overrideOrder;
        }

        // Fall back to [BehaviorOrder] attribute (cached — attributes are immutable in runtime)
        return s_behaviorOrderCache.GetOrAdd(
            behaviorType,
            static t => t.GetCustomAttribute<BehaviorOrderAttribute>(inherit: false)?.Order ?? 0);
    }

    /// <summary>
    /// Clears the behavior order attribute cache. For testing only.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void ResetForTesting()
    {
        s_behaviorOrderCache.Clear();
    }
}
