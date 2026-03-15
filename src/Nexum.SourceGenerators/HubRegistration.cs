using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Immutable data model representing a discovered [NexumStreamHub] class and its associated stream handler methods.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// Value equality guaranteed by sealed record.
    /// </summary>
    internal sealed record HubRegistration(
        string HubFullyQualifiedName,
        string HubNamespace,
        string HubClassName,
        EquatableArray<HubMethodRegistration> Methods) : IEquatable<HubRegistration>;

    /// <summary>
    /// Immutable data model representing a single hub method to generate.
    /// </summary>
    internal sealed record HubMethodRegistration(
        string MethodName,
        string MessageFullyQualifiedName,
        string ResultFullyQualifiedName,
        HandlerKind Kind) : IEquatable<HubMethodRegistration>;
}
