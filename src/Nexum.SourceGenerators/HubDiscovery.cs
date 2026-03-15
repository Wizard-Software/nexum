namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Intermediate data model representing a discovered [NexumStreamHub] class.
    /// Captures hub identity only — method matching happens at RegisterSourceOutput time
    /// by correlating with discovered stream handler registrations.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// </summary>
    internal readonly record struct HubDiscovery(
        string HubFullyQualifiedName,
        string HubNamespace,
        string HubClassName,
        /// <summary>
        /// FQNs of stream messages whose handlers this hub should expose as hub methods.
        /// Populated from the hub class's [NexumStreamHub] attribute arguments (if any),
        /// or left empty to mean "all discovered stream handlers in the compilation".
        /// </summary>
        EquatableArray<string> StreamHandlerMessageFQNs);
}
