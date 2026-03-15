namespace Nexum.Examples.Observability.Domain;

/// <summary>Represents a note in the system.</summary>
public sealed record Note(Guid Id, string Title, string Content);
