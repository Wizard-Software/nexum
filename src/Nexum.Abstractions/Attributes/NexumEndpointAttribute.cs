namespace Nexum.Abstractions;

/// <summary>
/// Marks a command or query type for automatic Minimal API endpoint generation.
/// When used with the Nexum Source Generator, this attribute causes a
/// <c>MapNexumEndpoints()</c> extension method to be generated that maps
/// the decorated type to an HTTP endpoint.
/// </summary>
/// <remarks>
/// <para>
/// The attribute is placed on command/query types (not handlers),
/// following the same pattern as <see cref="CommandHandlerAttribute"/> on handler types.
/// </para>
/// <para>
/// <b>Commands (POST/PUT/DELETE/PATCH):</b> The request body is deserialized as the command type.
/// </para>
/// <para>
/// <b>Queries (GET):</b> Properties are bound from route and query string parameters
/// using ASP.NET Core's <c>[AsParameters]</c> attribute binding.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NexumEndpointAttribute : Attribute
{
    /// <summary>
    /// Gets the HTTP method for this endpoint.
    /// </summary>
    public NexumHttpMethod Method { get; }

    /// <summary>
    /// Gets the route pattern for this endpoint (e.g., "/api/orders" or "/api/orders/{id}").
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets or sets an optional endpoint name for link generation.
    /// If not set, the Source Generator derives the name from the type name
    /// (stripping "Command" or "Query" suffix).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets an optional group/tag name for OpenAPI documentation.
    /// </summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NexumEndpointAttribute"/> class.
    /// </summary>
    /// <param name="method">The HTTP method for this endpoint.</param>
    /// <param name="pattern">The route pattern (e.g., "/api/orders").</param>
    public NexumEndpointAttribute(NexumHttpMethod method, string pattern)
    {
        Method = method;
        Pattern = pattern;
    }
}
