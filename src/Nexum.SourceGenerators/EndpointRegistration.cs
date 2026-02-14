using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Immutable data model representing a discovered endpoint registration.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// Value equality guaranteed by sealed record.
    /// </summary>
    internal sealed record EndpointRegistration(
        string MessageFullyQualifiedName,
        string? ResultFullyQualifiedName,
        string HttpMethod,
        string Pattern,
        string? Name,
        string? GroupName,
        HandlerKind Kind,
        bool HasResultMembers,
        bool IsInvalid = false,
        string? DiagnosticTypeName = null) : IEquatable<EndpointRegistration>;
}
