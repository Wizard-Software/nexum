using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Benchmarks.Setup;

/// <summary>
/// Simulates a Source Generator-produced Tier 2 pipeline registry for benchmarks.
/// Contains compiled dispatch methods (no behaviors) — handler-only path.
/// </summary>
internal static class BenchPipelineRegistry
{
    private static readonly Dictionary<Type, string> s_commandMethods = new()
    {
        [typeof(BenchCommand)] = nameof(Dispatch_BenchCommand),
    };

    private static readonly Dictionary<Type, string> s_queryMethods = new()
    {
        [typeof(BenchQuery)] = nameof(Dispatch_BenchQuery),
    };

    public static string? GetCommandMethodName(Type commandType)
        => s_commandMethods.TryGetValue(commandType, out var name) ? name : null;

    public static string? GetQueryMethodName(Type queryType)
        => s_queryMethods.TryGetValue(queryType, out var name) ? name : null;

    public static bool IsCompiledBehavior(Type behaviorType)
    {
        _ = behaviorType;
        return false;
    }

    public static Type[] GetCompiledBehaviorTypes()
        => [];

    public static ValueTask<Guid> Dispatch_BenchCommand(
        BenchCommand command,
        IServiceProvider sp,
        CancellationToken ct)
    {
        var handler = sp.GetRequiredService<BenchCommandHandler>();
        return handler.HandleAsync(command, ct);
    }

    public static ValueTask<string> Dispatch_BenchQuery(
        BenchQuery query,
        IServiceProvider sp,
        CancellationToken ct)
    {
        var handler = sp.GetRequiredService<BenchQueryHandler>();
        return handler.HandleAsync(query, ct);
    }
}

/// <summary>
/// Simulates a Source Generator-produced Tier 2 pipeline registry with 3 compiled behaviors.
/// </summary>
internal static class BenchPipelineRegistryWithBehaviors
{
    private static readonly Dictionary<Type, string> s_commandMethods = new()
    {
        [typeof(BenchCommand)] = nameof(Dispatch_BenchCommand),
    };

    private static readonly HashSet<Type> s_compiledBehaviorTypes =
    [
        typeof(BenchCommandBehavior1),
        typeof(BenchCommandBehavior2),
        typeof(BenchCommandBehavior3),
    ];

    public static string? GetCommandMethodName(Type commandType)
        => s_commandMethods.TryGetValue(commandType, out var name) ? name : null;

    public static bool IsCompiledBehavior(Type behaviorType)
        => s_compiledBehaviorTypes.Contains(behaviorType);

    public static Type[] GetCompiledBehaviorTypes()
        => [.. s_compiledBehaviorTypes];

    public static ValueTask<Guid> Dispatch_BenchCommand(
        BenchCommand command,
        IServiceProvider sp,
        CancellationToken ct)
    {
        var handler = sp.GetRequiredService<BenchCommandHandler>();
        var behavior1 = sp.GetRequiredService<BenchCommandBehavior1>();
        var behavior2 = sp.GetRequiredService<BenchCommandBehavior2>();
        var behavior3 = sp.GetRequiredService<BenchCommandBehavior3>();

        return behavior1.HandleAsync(command,
            ct2 => behavior2.HandleAsync(command,
                ct3 => behavior3.HandleAsync(command,
                    ct4 => handler.HandleAsync(command, ct4),
                    ct3),
                ct2),
            ct);
    }
}
