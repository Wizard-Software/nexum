using System.Diagnostics.CodeAnalysis;
using Nexum.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> to map Nexum command and query endpoints.
/// </summary>
public static class NexumEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a POST endpoint that dispatches a command with a result.
    /// The command is deserialized from the request body.
    /// Automatically adds OpenAPI metadata: <c>Produces&lt;TResult&gt;(200)</c> and <c>ProducesProblem(400)</c>.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern (e.g., "/api/orders").</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
    [RequiresUnreferencedCode("MapPost uses reflection on the delegate and its parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [RequiresDynamicCode("MapPost may generate code for the delegate parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Covered by [RequiresUnreferencedCode] on this method — callers are warned.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Covered by [RequiresDynamicCode] on this method — callers are warned.")]
    public static RouteHandlerBuilder MapNexumCommand<TCommand, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TCommand : ICommand<TResult>
    {
        return endpoints.MapPost(pattern, async (
            TCommand command,
            ICommandDispatcher dispatcher,
            CancellationToken ct) =>
        {
            TResult result = await dispatcher.DispatchAsync(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName(DeriveEndpointName<TCommand>())
        .Produces<TResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Maps a POST endpoint that dispatches a void command (returns 204 No Content).
    /// The command is deserialized from the request body.
    /// Automatically adds OpenAPI metadata: <c>Produces(204)</c> and <c>ProducesProblem(400)</c>.
    /// </summary>
    /// <typeparam name="TCommand">The void command type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern (e.g., "/api/orders/cancel").</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
    [RequiresUnreferencedCode("MapPost uses reflection on the delegate and its parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [RequiresDynamicCode("MapPost may generate code for the delegate parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Covered by [RequiresUnreferencedCode] on this method — callers are warned.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Covered by [RequiresDynamicCode] on this method — callers are warned.")]
    public static RouteHandlerBuilder MapNexumCommand<TCommand>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TCommand : IVoidCommand
    {
        return endpoints.MapPost(pattern, async (
            TCommand command,
            ICommandDispatcher dispatcher,
            CancellationToken ct) =>
        {
            await dispatcher.DispatchAsync(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .WithName(DeriveEndpointName<TCommand>())
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Maps a GET endpoint that dispatches a query and returns the result.
    /// The query is constructed from route and query string parameters via <c>[AsParameters]</c>.
    /// Automatically adds OpenAPI metadata: <c>Produces&lt;TResult&gt;(200)</c> and <c>ProducesProblem(400)</c>.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The route pattern (e.g., "/api/orders/{id}").</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
    /// <remarks>
    /// The query type properties are bound from route values and query string parameters
    /// using ASP.NET Core's <c>[AsParameters]</c> attribute binding.
    /// <para>
    /// <b>Known limitation:</b> <c>[AsParameters]</c> may mark all properties as required
    /// in the OpenAPI metadata, including nullable or default-valued ones (ASP.NET Core issue #52881).
    /// Use explicit <c>[FromQuery]</c> attributes or a custom <c>BindAsync</c> method as a workaround.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("MapGet uses reflection on the delegate and its parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [RequiresDynamicCode("MapGet may generate code for the delegate parameters. Use AOT-safe endpoint registration for NativeAOT.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Covered by [RequiresUnreferencedCode] on this method — callers are warned.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Covered by [RequiresDynamicCode] on this method — callers are warned.")]
    public static RouteHandlerBuilder MapNexumQuery<TQuery, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TQuery : IQuery<TResult>
    {
        return endpoints.MapGet(pattern, async (
            [AsParameters] TQuery query,
            IQueryDispatcher dispatcher,
            CancellationToken ct) =>
        {
            TResult result = await dispatcher.DispatchAsync(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName(DeriveEndpointName<TQuery>())
        .Produces<TResult>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// Derives an endpoint name from a type by stripping "Command" or "Query" suffix.
    /// For example, <c>CreateOrderCommand</c> becomes <c>CreateOrder</c>.
    /// </summary>
    private static string DeriveEndpointName<T>()
    {
        string typeName = typeof(T).Name;

        if (typeName.EndsWith("Command", StringComparison.Ordinal))
        {
            return typeName[..^"Command".Length];
        }

        if (typeName.EndsWith("Query", StringComparison.Ordinal))
        {
            return typeName[..^"Query".Length];
        }

        return typeName;
    }
}
