using Microsoft.CodeAnalysis;

namespace Nexum.Migration.MediatR.Analyzers
{
    internal static class MediatRMigrationDiagnosticDescriptors
    {
        public static readonly DiagnosticDescriptor NEXUMM001_RequestWithoutNexumInterface = new(
            id: "NEXUMM001",
            title: "MediatR IRequest without Nexum ICommand/IQuery",
            messageFormat: "Type '{0}' implements MediatR.IRequest<{1}> but does not implement ICommand<{1}> or IQuery<{1}>. Consider adding the Nexum interface for migration.",
            category: "Nexum.Migration",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

        public static readonly DiagnosticDescriptor NEXUMM002_RequestHandlerWithoutNexumHandler = new(
            id: "NEXUMM002",
            title: "MediatR IRequestHandler without Nexum handler",
            messageFormat: "Type '{0}' implements MediatR.IRequestHandler<{1},{2}> but does not implement a corresponding Nexum handler (ICommandHandler or IQueryHandler). Consider migrating to Nexum.",
            category: "Nexum.Migration",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

        public static readonly DiagnosticDescriptor NEXUMM003_NotificationWithoutNexum = new(
            id: "NEXUMM003",
            title: "MediatR INotification without Nexum INotification",
            messageFormat: "Type '{0}' implements MediatR.INotification but does not implement Nexum.Abstractions.INotification. Consider adding the Nexum interface for migration.",
            category: "Nexum.Migration",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: "https://github.com/asawicki/Nexum",
            customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });
    }
}
