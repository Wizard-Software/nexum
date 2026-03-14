using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using Nexum.Abstractions;

namespace Nexum.Internal;

/// <summary>
/// Builds execution pipelines for commands and queries by chaining behaviors around handlers.
/// </summary>
/// <remarks>
/// On the cold path (first dispatch per command type with behaviors), PipelineBuilder sorts behaviors
/// by <see cref="BehaviorOrderAttribute"/> and builds a factory delegate capturing the sorted indices.
/// On the hot path (subsequent dispatches), the cached factory is used directly — sorting is skipped
/// and only fresh behavior instances are resolved from DI.
/// </remarks>
internal static class PipelineBuilder
{
    /// <summary>
    /// Cache for <see cref="BehaviorOrderAttribute"/> lookups per behavior type.
    /// Only caches attribute-based order (not overrides from <see cref="NexumOptions"/>).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, int> s_behaviorOrderCache = new();

    // ---------------------------------------------------------------------------
    // Pipeline factory builders — cold path produces a factory; hot path calls it
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Builds and returns a pipeline factory for commands whose sorted behavior structure is
    /// captured from the cold-path <paramref name="sortedBehaviors"/> array.
    /// The factory resolves fresh behavior instances from DI on every call without re-sorting.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sortedBehaviors">
    /// Sorted behavior instances produced on the cold path.
    /// Their concrete types define the pipeline execution order baked into the factory.
    /// </param>
    /// <returns>
    /// A factory <c>(command, handler, sp, ct) => ValueTask&lt;TResult&gt;</c> that resolves
    /// fresh behavior instances from DI each call and composes them in the cached order.
    /// </returns>
    internal static Func<TCommand, ICommandHandler<TCommand, TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>
        BuildCommandPipelineFactory<TCommand, TResult>(
            ICommandBehavior<TCommand, TResult>[] sortedBehaviors)
        where TCommand : ICommand<TResult>
    {
        // Capture the sorted type sequence as the pipeline structure.
        // MS DI always returns T[] for IEnumerable<T>, so type-order from the registration
        // is stable across dispatches. We match fresh instances by type on each call.
        Type[] sortedTypes = new Type[sortedBehaviors.Length];
        for (int i = 0; i < sortedBehaviors.Length; i++)
        {
            sortedTypes[i] = sortedBehaviors[i].GetType();
        }

        // Capture sortedTypes in closure — sorting is never repeated on hot path.
        return (command, handler, sp, ct) =>
        {
            var behaviorsObj = sp.GetService(typeof(IEnumerable<ICommandBehavior<TCommand, TResult>>));

            if (behaviorsObj is not ICommandBehavior<TCommand, TResult>[] freshArr || freshArr.Length == 0)
            {
                // Behaviors disappeared from DI after cold path (should not happen in normal usage)
                return handler.HandleAsync(command, ct);
            }

            // Build delegate chain using the pre-sorted type order.
            // When fresh array count matches, reorder by type for correct behavior ordering.
            // When there is a mismatch, fall back to DI registration order.
            ICommandBehavior<TCommand, TResult>[] orderedBehaviors =
                freshArr.Length == sortedTypes.Length
                    ? ReorderByTypes(freshArr, sortedTypes)
                    : freshArr;

            CommandHandlerDelegate<TResult> pipeline = ct2 => handler.HandleAsync(command, ct2);
            for (int i = orderedBehaviors.Length - 1; i >= 0; i--)
            {
                ICommandBehavior<TCommand, TResult> behavior = orderedBehaviors[i];
                CommandHandlerDelegate<TResult> next = pipeline;
                pipeline = ct2 => behavior.HandleAsync(command, next, ct2);
            }

            return pipeline(ct);
        };
    }

    /// <summary>
    /// Builds and returns a pipeline factory for queries whose sorted behavior structure is
    /// captured from the cold-path <paramref name="sortedBehaviors"/> array.
    /// The factory resolves fresh behavior instances from DI on every call without re-sorting.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="sortedBehaviors">
    /// Sorted behavior instances produced on the cold path.
    /// Their concrete types define the pipeline execution order baked into the factory.
    /// </param>
    /// <returns>
    /// A factory <c>(query, handler, sp, ct) => ValueTask&lt;TResult&gt;</c> that resolves
    /// fresh behavior instances from DI each call and composes them in the cached order.
    /// </returns>
    internal static Func<TQuery, IQueryHandler<TQuery, TResult>, IServiceProvider, CancellationToken, ValueTask<TResult>>
        BuildQueryPipelineFactory<TQuery, TResult>(
            IQueryBehavior<TQuery, TResult>[] sortedBehaviors)
        where TQuery : IQuery<TResult>
    {
        Type[] sortedTypes = new Type[sortedBehaviors.Length];
        for (int i = 0; i < sortedBehaviors.Length; i++)
        {
            sortedTypes[i] = sortedBehaviors[i].GetType();
        }

        return (query, handler, sp, ct) =>
        {
            var behaviorsObj = sp.GetService(typeof(IEnumerable<IQueryBehavior<TQuery, TResult>>));

            if (behaviorsObj is not IQueryBehavior<TQuery, TResult>[] freshArr || freshArr.Length == 0)
            {
                return handler.HandleAsync(query, ct);
            }

            IQueryBehavior<TQuery, TResult>[] orderedBehaviors =
                freshArr.Length == sortedTypes.Length
                    ? ReorderByTypes(freshArr, sortedTypes)
                    : freshArr;

            QueryHandlerDelegate<TResult> pipeline = ct2 => handler.HandleAsync(query, ct2);
            for (int i = orderedBehaviors.Length - 1; i >= 0; i--)
            {
                IQueryBehavior<TQuery, TResult> behavior = orderedBehaviors[i];
                QueryHandlerDelegate<TResult> next = pipeline;
                pipeline = ct2 => behavior.HandleAsync(query, next, ct2);
            }

            return pipeline(ct);
        };
    }

    /// <summary>
    /// Builds and returns a pipeline factory for stream queries whose sorted behavior structure is
    /// captured from the cold-path <paramref name="sortedBehaviors"/> array.
    /// The factory resolves fresh behavior instances from DI on every call without re-sorting.
    /// </summary>
    /// <typeparam name="TQuery">The streaming query type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="sortedBehaviors">
    /// Sorted behavior instances produced on the cold path.
    /// Their concrete types define the pipeline execution order baked into the factory.
    /// </param>
    /// <returns>
    /// A factory <c>(query, handler, sp, ct) => IAsyncEnumerable&lt;TResult&gt;</c> that resolves
    /// fresh behavior instances from DI each call and composes them in the cached order.
    /// </returns>
    internal static Func<TQuery, IStreamQueryHandler<TQuery, TResult>, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>>
        BuildStreamQueryPipelineFactory<TQuery, TResult>(
            IStreamQueryBehavior<TQuery, TResult>[] sortedBehaviors)
        where TQuery : IStreamQuery<TResult>
    {
        Type[] sortedTypes = new Type[sortedBehaviors.Length];
        for (int i = 0; i < sortedBehaviors.Length; i++)
        {
            sortedTypes[i] = sortedBehaviors[i].GetType();
        }

        return (query, handler, sp, ct) =>
        {
            var behaviorsObj = sp.GetService(typeof(IEnumerable<IStreamQueryBehavior<TQuery, TResult>>));

            if (behaviorsObj is not IStreamQueryBehavior<TQuery, TResult>[] freshArr || freshArr.Length == 0)
            {
                return handler.HandleAsync(query, ct);
            }

            IStreamQueryBehavior<TQuery, TResult>[] orderedBehaviors =
                freshArr.Length == sortedTypes.Length
                    ? ReorderByTypes(freshArr, sortedTypes)
                    : freshArr;

            StreamQueryHandlerDelegate<TResult> pipeline = ct2 => handler.HandleAsync(query, ct2);
            for (int i = orderedBehaviors.Length - 1; i >= 0; i--)
            {
                IStreamQueryBehavior<TQuery, TResult> behavior = orderedBehaviors[i];
                StreamQueryHandlerDelegate<TResult> next = pipeline;
                pipeline = ct2 => behavior.HandleAsync(query, next, ct2);
            }

            return pipeline(ct);
        };
    }

    // ---------------------------------------------------------------------------
    // Full pipeline builders — used on cold path and by WrapWith* (Tier 2 path)
    // ---------------------------------------------------------------------------

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
    /// Builds a command pipeline, and also outputs the sorted behavior array so the caller
    /// can cache a pipeline factory capturing the sort order for subsequent dispatches.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="command">The command instance to be handled.</param>
    /// <param name="handler">The resolved command handler.</param>
    /// <param name="behaviors">Pre-resolved behaviors from DI (MS DI returns T[]).</param>
    /// <param name="options">Nexum runtime options.</param>
    /// <param name="sortedBehaviorsOut">
    /// On return, contains the behaviors in execution order (outermost first).
    /// Used by the caller to create a pipeline factory via
    /// <see cref="BuildCommandPipelineFactory{TCommand,TResult}"/>.
    /// </param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    internal static CommandHandlerDelegate<TResult> BuildCommandPipelineAndCaptureSorted<TCommand, TResult>(
        TCommand command,
        ICommandHandler<TCommand, TResult> handler,
        ICommandBehavior<TCommand, TResult>[] behaviors,
        NexumOptions options,
        out ICommandBehavior<TCommand, TResult>[] sortedBehaviorsOut)
        where TCommand : ICommand<TResult>
    {
        // Sort behaviors (this is the cold path — one-time cost)
        var sortedList = new List<(ICommandBehavior<TCommand, TResult> Behavior, int Order, int Index)>(behaviors.Length);
        for (int i = 0; i < behaviors.Length; i++)
        {
            sortedList.Add((behaviors[i], GetBehaviorOrder(behaviors[i], options), i));
        }

        sortedList.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Output sorted array so caller can build factory
        sortedBehaviorsOut = new ICommandBehavior<TCommand, TResult>[sortedList.Count];
        for (int i = 0; i < sortedList.Count; i++)
        {
            sortedBehaviorsOut[i] = sortedList[i].Behavior;
        }

        // Build the pipeline from the sorted order
        CommandHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(command, ct);
        for (int i = sortedBehaviorsOut.Length - 1; i >= 0; i--)
        {
            ICommandBehavior<TCommand, TResult> behavior = sortedBehaviorsOut[i];
            CommandHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(command, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// Builds a query pipeline, and also outputs the sorted behavior array so the caller
    /// can cache a pipeline factory capturing the sort order for subsequent dispatches.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved query handler.</param>
    /// <param name="behaviors">Pre-resolved behaviors from DI (MS DI returns T[]).</param>
    /// <param name="options">Nexum runtime options.</param>
    /// <param name="sortedBehaviorsOut">
    /// On return, contains the behaviors in execution order (outermost first).
    /// </param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    internal static QueryHandlerDelegate<TResult> BuildQueryPipelineAndCaptureSorted<TQuery, TResult>(
        TQuery query,
        IQueryHandler<TQuery, TResult> handler,
        IQueryBehavior<TQuery, TResult>[] behaviors,
        NexumOptions options,
        out IQueryBehavior<TQuery, TResult>[] sortedBehaviorsOut)
        where TQuery : IQuery<TResult>
    {
        var sortedList = new List<(IQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>(behaviors.Length);
        for (int i = 0; i < behaviors.Length; i++)
        {
            sortedList.Add((behaviors[i], GetBehaviorOrder(behaviors[i], options), i));
        }

        sortedList.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        sortedBehaviorsOut = new IQueryBehavior<TQuery, TResult>[sortedList.Count];
        for (int i = 0; i < sortedList.Count; i++)
        {
            sortedBehaviorsOut[i] = sortedList[i].Behavior;
        }

        QueryHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(query, ct);
        for (int i = sortedBehaviorsOut.Length - 1; i >= 0; i--)
        {
            IQueryBehavior<TQuery, TResult> behavior = sortedBehaviorsOut[i];
            QueryHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline;
    }

    /// <summary>
    /// Builds a stream query pipeline, and also outputs the sorted behavior array so the caller
    /// can cache a pipeline factory capturing the sort order for subsequent dispatches.
    /// </summary>
    /// <typeparam name="TQuery">The streaming query type.</typeparam>
    /// <typeparam name="TResult">The result element type.</typeparam>
    /// <param name="query">The query instance to be handled.</param>
    /// <param name="handler">The resolved streaming query handler.</param>
    /// <param name="behaviors">Pre-resolved behaviors from DI (MS DI returns T[]).</param>
    /// <param name="options">Nexum runtime options.</param>
    /// <param name="sortedBehaviorsOut">
    /// On return, contains the behaviors in execution order (outermost first).
    /// </param>
    /// <returns>A delegate representing the outermost layer of the pipeline.</returns>
    internal static StreamQueryHandlerDelegate<TResult> BuildStreamQueryPipelineAndCaptureSorted<TQuery, TResult>(
        TQuery query,
        IStreamQueryHandler<TQuery, TResult> handler,
        IStreamQueryBehavior<TQuery, TResult>[] behaviors,
        NexumOptions options,
        out IStreamQueryBehavior<TQuery, TResult>[] sortedBehaviorsOut)
        where TQuery : IStreamQuery<TResult>
    {
        var sortedList = new List<(IStreamQueryBehavior<TQuery, TResult> Behavior, int Order, int Index)>(behaviors.Length);
        for (int i = 0; i < behaviors.Length; i++)
        {
            sortedList.Add((behaviors[i], GetBehaviorOrder(behaviors[i], options), i));
        }

        sortedList.Sort((a, b) =>
        {
            int cmp = a.Order.CompareTo(b.Order);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        sortedBehaviorsOut = new IStreamQueryBehavior<TQuery, TResult>[sortedList.Count];
        for (int i = 0; i < sortedList.Count; i++)
        {
            sortedBehaviorsOut[i] = sortedList[i].Behavior;
        }

        StreamQueryHandlerDelegate<TResult> pipeline = ct => handler.HandleAsync(query, ct);
        for (int i = sortedBehaviorsOut.Length - 1; i >= 0; i--)
        {
            IStreamQueryBehavior<TQuery, TResult> behavior = sortedBehaviorsOut[i];
            StreamQueryHandlerDelegate<TResult> next = pipeline;
            pipeline = ct => behavior.HandleAsync(query, next, ct);
        }

        return pipeline;
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

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

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
    /// Reorders <paramref name="freshArr"/> to match the type sequence in <paramref name="sortedTypes"/>.
    /// Uses O(n²) matching — safe for typical behavior counts (≤5).
    /// When a type in <paramref name="sortedTypes"/> is not found in <paramref name="freshArr"/>
    /// (e.g. after a test reset), the original DI order is returned unchanged as a safe fallback.
    /// </summary>
    private static T[] ReorderByTypes<T>(T[] freshArr, Type[] sortedTypes) where T : class
    {
        var reordered = new T[sortedTypes.Length];
        var used = new bool[freshArr.Length];

        for (int s = 0; s < sortedTypes.Length; s++)
        {
            Type targetType = sortedTypes[s];
            bool found = false;
            for (int f = 0; f < freshArr.Length; f++)
            {
                if (!used[f] && freshArr[f].GetType() == targetType)
                {
                    reordered[s] = freshArr[f];
                    used[f] = true;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // Type mismatch after cold path (possible after ResetForTesting) — return original array
                return freshArr;
            }
        }

        return reordered;
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
