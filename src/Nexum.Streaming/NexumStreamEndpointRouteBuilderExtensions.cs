using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexum.Abstractions;

namespace Nexum.Streaming;

/// <summary>
/// Extension methods for <see cref="IEndpointRouteBuilder"/> to map Nexum stream queries as SSE endpoints.
/// </summary>
public static class NexumStreamEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps a stream query to a Server-Sent Events (SSE) endpoint using .NET 10 native SSE support.
    /// </summary>
    /// <typeparam name="TQuery">The stream query type. Must implement <see cref="IStreamQuery{TResult}"/>.</typeparam>
    /// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the endpoint (e.g., <c>/api/orders/stream</c>).</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> for further endpoint configuration.</returns>
    /// <remarks>
    /// <para>
    /// The query type properties are bound from route values and query string parameters
    /// using ASP.NET Core's <c>[AsParameters]</c> attribute binding.
    /// </para>
    /// <para>
    /// Uses <see cref="TypedResults.ServerSentEvents{T}(System.Collections.Generic.IAsyncEnumerable{T}, string?)"/>
    /// to emit each stream element as a JSON-serialized SSE <c>data:</c> field. The framework automatically
    /// sets <c>Content-Type: text/event-stream</c> and handles SSE framing.
    /// </para>
    /// <para>
    /// The <see cref="CancellationToken"/> from the HTTP connection lifetime is propagated to the
    /// underlying stream — closing the browser tab or network connection terminates the query handler.
    /// </para>
    /// <para>
    /// <b>Known limitation:</b> <c>[AsParameters]</c> may mark all properties as required
    /// in the OpenAPI metadata, including nullable or default-valued ones (ASP.NET Core issue #52881).
    /// Use explicit <c>[FromQuery]</c> attributes or a custom <c>BindAsync</c> method as a workaround.
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder MapNexumStream<TQuery, TResult>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TQuery : IStreamQuery<TResult>
    {
        return endpoints.MapGet(pattern, (
            [AsParameters] TQuery query,
            IQueryDispatcher dispatcher,
            CancellationToken ct) =>
        {
            IAsyncEnumerable<TResult> stream = dispatcher.StreamAsync<TResult>(query, ct);
            return TypedResults.ServerSentEvents(stream);
        })
        .WithName(DeriveEndpointName<TQuery>())
        .Produces<TResult>(StatusCodes.Status200OK, "text/event-stream");
    }

    /// <summary>
    /// Derives an endpoint name from a query type by stripping the "Query" suffix.
    /// For example, <c>GetOrderUpdatesQuery</c> becomes <c>GetOrderUpdates</c>.
    /// </summary>
    private static string DeriveEndpointName<T>()
    {
        string typeName = typeof(T).Name;

        if (typeName.EndsWith("Query", StringComparison.Ordinal))
        {
            return typeName[..^"Query".Length];
        }

        return typeName;
    }
}
