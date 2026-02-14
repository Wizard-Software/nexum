using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Immutable data model representing a discovered handler registration.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// Value equality guaranteed by sealed record.
    /// </summary>
    internal sealed record HandlerRegistration(
        string HandlerFullyQualifiedName,
        string ServiceInterfaceFullyQualifiedName,
        string MessageFullyQualifiedName,
        string? ResultFullyQualifiedName,
        HandlerKind Kind,
        string? Lifetime,
        bool IsInvalid = false,
        string? DiagnosticTypeName = null,
        string? DiagnosticAttributeName = null,
        string? DiagnosticExpectedInterface = null) : IEquatable<HandlerRegistration>;
}
