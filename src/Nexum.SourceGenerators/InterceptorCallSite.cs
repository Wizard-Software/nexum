using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Immutable data model representing a discovered interceptor call-site.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// </summary>
    internal readonly record struct InterceptorCallSite(
        InterceptorKind Kind,
        string MessageFullyQualifiedName,
        string ResultFullyQualifiedName,
        string InterceptsLocationSyntax,
        string DispatcherInterfaceFullyQualifiedName,
        string? SkipReason = null) : IEquatable<InterceptorCallSite>;
}
