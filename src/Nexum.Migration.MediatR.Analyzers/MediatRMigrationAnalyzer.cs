using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nexum.Migration.MediatR.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed class MediatRMigrationAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                MediatRMigrationDiagnosticDescriptors.NEXUMM001_RequestWithoutNexumInterface,
                MediatRMigrationDiagnosticDescriptors.NEXUMM002_RequestHandlerWithoutNexumHandler,
                MediatRMigrationDiagnosticDescriptors.NEXUMM003_NotificationWithoutNexum);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            Compilation compilation = context.Compilation;

            // Resolve MediatR types by fully-qualified metadata name
            INamedTypeSymbol? mediatRIRequest = compilation.GetTypeByMetadataName("MediatR.IRequest`1");
            INamedTypeSymbol? mediatRIRequestHandler = compilation.GetTypeByMetadataName("MediatR.IRequestHandler`2");
            INamedTypeSymbol? mediatRINotification = compilation.GetTypeByMetadataName("MediatR.INotification");

            // If MediatR is not referenced, skip analysis entirely
            if (mediatRIRequest is null || mediatRIRequestHandler is null || mediatRINotification is null)
            {
                return;
            }

            // Resolve Nexum types by fully-qualified metadata name
            INamedTypeSymbol? nexumICommand = compilation.GetTypeByMetadataName("Nexum.Abstractions.ICommand`1");
            INamedTypeSymbol? nexumIQuery = compilation.GetTypeByMetadataName("Nexum.Abstractions.IQuery`1");
            INamedTypeSymbol? nexumICommandHandler = compilation.GetTypeByMetadataName("Nexum.Abstractions.ICommandHandler`2");
            INamedTypeSymbol? nexumIQueryHandler = compilation.GetTypeByMetadataName("Nexum.Abstractions.IQueryHandler`2");
            INamedTypeSymbol? nexumINotification = compilation.GetTypeByMetadataName("Nexum.Abstractions.INotification");

            // Collect all named types from the compilation's source
            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            foreach (INamedTypeSymbol type in allTypes)
            {
                if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                {
                    continue;
                }

                // Skip open generic type definitions
                if (type.IsGenericType && type.IsUnboundGenericType)
                {
                    continue;
                }

                AnalyzeType(
                    context,
                    type,
                    mediatRIRequest,
                    mediatRIRequestHandler,
                    mediatRINotification,
                    nexumICommand,
                    nexumIQuery,
                    nexumICommandHandler,
                    nexumIQueryHandler,
                    nexumINotification);
            }
        }

        private static void AnalyzeType(
            CompilationAnalysisContext context,
            INamedTypeSymbol type,
            INamedTypeSymbol mediatRIRequest,
            INamedTypeSymbol mediatRIRequestHandler,
            INamedTypeSymbol mediatRINotification,
            INamedTypeSymbol? nexumICommand,
            INamedTypeSymbol? nexumIQuery,
            INamedTypeSymbol? nexumICommandHandler,
            INamedTypeSymbol? nexumIQueryHandler,
            INamedTypeSymbol? nexumINotification)
        {
            bool implementsNexumCommand = false;
            bool implementsNexumQuery = false;
            bool implementsNexumCommandHandler = false;
            bool implementsNexumQueryHandler = false;
            bool implementsNexumNotification = false;

            INamedTypeSymbol? mediatRRequestInterface = null;
            INamedTypeSymbol? mediatRRequestHandlerInterface = null;
            bool implementsMediatRNotification = false;

            foreach (INamedTypeSymbol iface in type.AllInterfaces)
            {
                INamedTypeSymbol originalDef = iface.OriginalDefinition;

                if (iface.IsGenericType)
                {
                    if (SymbolEqualityComparer.Default.Equals(originalDef, mediatRIRequest))
                    {
                        mediatRRequestInterface = iface;
                    }
                    else if (SymbolEqualityComparer.Default.Equals(originalDef, mediatRIRequestHandler))
                    {
                        mediatRRequestHandlerInterface = iface;
                    }
                    else if (nexumICommand is not null && SymbolEqualityComparer.Default.Equals(originalDef, nexumICommand))
                    {
                        implementsNexumCommand = true;
                    }
                    else if (nexumIQuery is not null && SymbolEqualityComparer.Default.Equals(originalDef, nexumIQuery))
                    {
                        implementsNexumQuery = true;
                    }
                    else if (nexumICommandHandler is not null && SymbolEqualityComparer.Default.Equals(originalDef, nexumICommandHandler))
                    {
                        implementsNexumCommandHandler = true;
                    }
                    else if (nexumIQueryHandler is not null && SymbolEqualityComparer.Default.Equals(originalDef, nexumIQueryHandler))
                    {
                        implementsNexumQueryHandler = true;
                    }
                }
                else
                {
                    if (SymbolEqualityComparer.Default.Equals(originalDef, mediatRINotification))
                    {
                        implementsMediatRNotification = true;
                    }
                    else if (nexumINotification is not null && SymbolEqualityComparer.Default.Equals(originalDef, nexumINotification))
                    {
                        implementsNexumNotification = true;
                    }
                }
            }

            // NEXUMM001: MediatR IRequest<T> without Nexum ICommand<T> or IQuery<T>
            if (mediatRRequestInterface is not null && !implementsNexumCommand && !implementsNexumQuery)
            {
                string resultTypeName = mediatRRequestInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                context.ReportDiagnostic(Diagnostic.Create(
                    MediatRMigrationDiagnosticDescriptors.NEXUMM001_RequestWithoutNexumInterface,
                    type.Locations.FirstOrDefault() ?? Location.None,
                    type.Name,
                    resultTypeName));
            }

            // NEXUMM002: MediatR IRequestHandler<,> without Nexum ICommandHandler or IQueryHandler
            if (mediatRRequestHandlerInterface is not null && !implementsNexumCommandHandler && !implementsNexumQueryHandler)
            {
                string requestTypeName = mediatRRequestHandlerInterface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string responseTypeName = mediatRRequestHandlerInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                context.ReportDiagnostic(Diagnostic.Create(
                    MediatRMigrationDiagnosticDescriptors.NEXUMM002_RequestHandlerWithoutNexumHandler,
                    type.Locations.FirstOrDefault() ?? Location.None,
                    type.Name,
                    requestTypeName,
                    responseTypeName));
            }

            // NEXUMM003: MediatR INotification without Nexum INotification
            if (implementsMediatRNotification && !implementsNexumNotification)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MediatRMigrationDiagnosticDescriptors.NEXUMM003_NotificationWithoutNexum,
                    type.Locations.FirstOrDefault() ?? Location.None,
                    type.Name));
            }
        }

        private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> types)
        {
            foreach (ISymbol member in ns.GetMembers())
            {
                if (member is INamedTypeSymbol type)
                {
                    types.Add(type);
                    CollectNestedTypes(type, types);
                }
                else if (member is INamespaceSymbol childNs)
                {
                    CollectTypes(childNs, types);
                }
            }
        }

        private static void CollectNestedTypes(INamedTypeSymbol type, List<INamedTypeSymbol> types)
        {
            foreach (INamedTypeSymbol member in type.GetTypeMembers())
            {
                types.Add(member);
                CollectNestedTypes(member, types);
            }
        }
    }
}
