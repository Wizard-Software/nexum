using System;

namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Intermediate data model representing a discovered [NexumStreamHub] class.
    /// Captures hub identity only — method matching happens at RegisterSourceOutput time
    /// by correlating with discovered stream handler registrations.
    /// All fields are strings (no ISymbol/SyntaxNode references — CONSTITUTION N9).
    /// </summary>
    internal readonly struct HubDiscovery : IEquatable<HubDiscovery>
    {
        public string HubFullyQualifiedName { get; }
        public string HubNamespace { get; }
        public string HubClassName { get; }

        /// <summary>
        /// FQNs of stream messages whose handlers this hub should expose as hub methods.
        /// Populated from the hub class's [NexumStreamHub] attribute arguments (if any),
        /// or left empty to mean "all discovered stream handlers in the compilation".
        /// </summary>
        public EquatableArray<string> StreamHandlerMessageFQNs { get; }

        public HubDiscovery(
            string hubFullyQualifiedName,
            string hubNamespace,
            string hubClassName,
            EquatableArray<string> streamHandlerMessageFQNs)
        {
            HubFullyQualifiedName = hubFullyQualifiedName;
            HubNamespace = hubNamespace;
            HubClassName = hubClassName;
            StreamHandlerMessageFQNs = streamHandlerMessageFQNs;
        }

        public bool Equals(HubDiscovery other)
        {
            return HubFullyQualifiedName == other.HubFullyQualifiedName
                && HubNamespace == other.HubNamespace
                && HubClassName == other.HubClassName
                && StreamHandlerMessageFQNs == other.StreamHandlerMessageFQNs;
        }

        public override bool Equals(object? obj)
        {
            return obj is HubDiscovery other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = HubFullyQualifiedName?.GetHashCode() ?? 0;
                hash = (hash * 31) + (HubNamespace?.GetHashCode() ?? 0);
                hash = (hash * 31) + (HubClassName?.GetHashCode() ?? 0);
                hash = (hash * 31) + StreamHandlerMessageFQNs.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(HubDiscovery left, HubDiscovery right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HubDiscovery left, HubDiscovery right)
        {
            return !left.Equals(right);
        }
    }
}
