namespace Nexum.Abstractions;

/// <summary>
/// Specifies the HTTP method for a Nexum endpoint.
/// Used by <see cref="NexumEndpointAttribute"/> for source-generated endpoint mapping.
/// </summary>
/// <remarks>
/// Uses a dedicated enum instead of <see cref="System.Net.Http.HttpMethod"/>
/// to maintain zero dependencies in the Abstractions package.
/// </remarks>
public enum NexumHttpMethod
{
    /// <summary>HTTP GET — typically used for queries.</summary>
    Get,

    /// <summary>HTTP POST — typically used for commands that create resources.</summary>
    Post,

    /// <summary>HTTP PUT — typically used for commands that replace resources.</summary>
    Put,

    /// <summary>HTTP DELETE — typically used for commands that remove resources.</summary>
    Delete,

    /// <summary>HTTP PATCH — typically used for commands that partially update resources.</summary>
    Patch
}
