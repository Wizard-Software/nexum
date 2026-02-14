using Microsoft.CodeAnalysis;

namespace Nexum.SourceGenerators
{
    internal static class DiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor NEXUM001_NoHandlerFound = new(
            id: "NEXUM001",
            title: "No handler found for message type",
            messageFormat: "No handler found for {0} type '{1}'. Add a handler class with [{2}] attribute.",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

        public static readonly DiagnosticDescriptor NEXUM002_DuplicateHandler = new(
            id: "NEXUM002",
            title: "Duplicate handler for message type",
            messageFormat: "Multiple handlers found for {0} type '{1}': '{2}', '{3}'. Only one handler per command/query is allowed.",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum");

        public static readonly DiagnosticDescriptor NEXUM003_MissingAttribute = new(
            id: "NEXUM003",
            title: "Handler missing marker attribute",
            messageFormat: "Type '{0}' implements '{1}' but is missing [{2}] attribute. The handler will not be auto-registered.",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

        public static readonly DiagnosticDescriptor NEXUM004_AttributeWithoutInterface = new(
            id: "NEXUM004",
            title: "Marker attribute without handler interface",
            messageFormat: "Type '{0}' has [{1}] attribute but does not implement '{2}'. The attribute will be ignored.",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum");

        public static readonly DiagnosticDescriptor NEXUM005_InterceptorGenerated = new(
            id: "NEXUM005",
            title: "Interceptor generated for dispatch call-site",
            messageFormat: "Interceptor generated for {0} dispatch of '{1}' at {2}:{3}",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum");

        public static readonly DiagnosticDescriptor NEXUM006_CannotInterceptDispatch = new(
            id: "NEXUM006",
            title: "Cannot intercept dispatch — concrete type unknown",
            messageFormat: "Cannot intercept dispatch of '{0}' — concrete message type cannot be determined at compile time. The call will use the runtime dispatch path.",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum");

        public static readonly DiagnosticDescriptor NEXUM007_InterceptorSkippedNoHandler = new(
            id: "NEXUM007",
            title: "Interceptor skipped — handler not in compilation",
            messageFormat: "Dispatch call to '{0}' ({1}) at this location will use runtime/Tier 2 fallback because no handler was found in the current compilation",
            category: "Nexum.SourceGenerators",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum");
    }
}
