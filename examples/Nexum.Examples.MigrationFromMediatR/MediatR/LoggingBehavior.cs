using System.Diagnostics;

namespace Nexum.Examples.MigrationFromMediatR.MediatR;

// BEFORE (MediatR pipeline behavior): This IPipelineBehavior logs request timing.
// AddNexumWithMediatRCompat() automatically adapts IPipelineBehavior<,> instances
// to Nexum's ICommandBehavior/IQueryBehavior — no manual migration needed for behaviors.
public sealed class LoggingBehavior<TRequest, TResponse>
    : global::MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        global::MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var typeName = typeof(TRequest).Name;
        Console.WriteLine($"  [MediatR Behavior] Handling {typeName}...");

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        Console.WriteLine($"  [MediatR Behavior] {typeName} completed in {sw.ElapsedMilliseconds}ms");
        return response;
    }
}
