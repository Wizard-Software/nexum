using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Immutable data model representing a discovered behavior registration.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// Value equality guaranteed by sealed record.
    /// </summary>
    internal sealed record BehaviorRegistration(
        string BehaviorFullyQualifiedName,
        BehaviorKind Kind,
        int Order,
        bool IsOpenGeneric,
        string? MessageFullyQualifiedName,
        string? ResultFullyQualifiedName,
        string ServiceInterfaceFullyQualifiedName) : IEquatable<BehaviorRegistration>;
}
