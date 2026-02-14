using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Nexum.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal sealed class MissingHandlerDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        private const string NexumAbstractionsNs = "Nexum.Abstractions";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                DiagnosticDescriptors.NEXUM001_NoHandlerFound,
                DiagnosticDescriptors.NEXUM003_MissingAttribute);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationAction(AnalyzeCompilation);
        }

        private static void AnalyzeCompilation(CompilationAnalysisContext context)
        {
            Compilation compilation = context.Compilation;

            // Collect all named types from the compilation's source
            var allTypes = new List<INamedTypeSymbol>();
            CollectTypes(compilation.GlobalNamespace, allTypes);

            // Map: message FQN -> set of handler types with correct attribute
            var handledMessageTypes = new Dictionary<string, HashSet<string>>();

            // For NEXUM003: types implementing handler interface without attribute
            var handlersWithoutAttribute = new List<(INamedTypeSymbol Type, string InterfaceName, string ExpectedAttribute)>();

            foreach (INamedTypeSymbol type in allTypes)
            {
                if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                {
                    continue;
                }

                // Skip open generic type definitions
                if (IsOpenGenericType(type))
                {
                    continue;
                }

                // Check each handler interface the type implements
                foreach (INamedTypeSymbol iface in type.AllInterfaces)
                {
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    INamedTypeSymbol originalDef = iface.OriginalDefinition;
                    string? ns = GetNamespace(originalDef.ContainingNamespace);
                    if (ns != NexumAbstractionsNs)
                    {
                        continue;
                    }

                    string name = originalDef.Name;
                    int arity = originalDef.Arity;

                    (string kind, string attrName)? match = null;

                    if (name == "ICommandHandler" && arity == 2)
                    {
                        match = ("Command", "CommandHandlerAttribute");
                    }
                    else if (name == "IQueryHandler" && arity == 2)
                    {
                        match = ("Query", "QueryHandlerAttribute");
                    }
                    else if (name == "IStreamQueryHandler" && arity == 2)
                    {
                        match = ("StreamQuery", "StreamQueryHandlerAttribute");
                    }
                    else if (name == "INotificationHandler" && arity == 1)
                    {
                        match = ("Notification", "NotificationHandlerAttribute");
                    }

                    if (match is null)
                    {
                        continue;
                    }

                    // Check if type has the corresponding attribute
                    bool hasAttr = HasAttribute(type, NexumAbstractionsNs, match.Value.attrName);

                    if (hasAttr)
                    {
                        // Record that this message type has a handler
                        string messageFQN = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        string handlerFQN = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                        if (!handledMessageTypes.TryGetValue(messageFQN, out HashSet<string>? handlers))
                        {
                            handlers = new HashSet<string>();
                            handledMessageTypes[messageFQN] = handlers;
                        }
                        handlers.Add(handlerFQN);
                    }
                    else
                    {
                        // NEXUM003 candidate
                        string interfaceDisplay = iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        handlersWithoutAttribute.Add((type, interfaceDisplay, match.Value.attrName));
                    }
                }
            }

            // Report NEXUM003
            foreach ((INamedTypeSymbol type, string interfaceName, string expectedAttr) in handlersWithoutAttribute)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NEXUM003_MissingAttribute,
                    type.Locations.FirstOrDefault() ?? Location.None,
                    type.Name,
                    interfaceName,
                    expectedAttr.Replace("Attribute", "")));
            }

            // NEXUM001: Check message types that don't have handlers
            foreach (INamedTypeSymbol type in allTypes)
            {
                if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                {
                    continue;
                }

                if (IsOpenGenericType(type))
                {
                    continue;
                }

                foreach (INamedTypeSymbol iface in type.AllInterfaces)
                {
                    if (!iface.IsGenericType)
                    {
                        continue;
                    }

                    INamedTypeSymbol originalDef = iface.OriginalDefinition;
                    string? ns = GetNamespace(originalDef.ContainingNamespace);
                    if (ns != NexumAbstractionsNs)
                    {
                        continue;
                    }

                    string name = originalDef.Name;
                    int arity = originalDef.Arity;

                    string? kindLabel = null;
                    string? expectedAttr = null;

                    // Only check command, query, stream query (not notification)
                    if (name == "ICommand" && arity == 1)
                    {
                        kindLabel = "command";
                        expectedAttr = "CommandHandler";
                    }
                    else if (name == "IQuery" && arity == 1)
                    {
                        kindLabel = "query";
                        expectedAttr = "QueryHandler";
                    }
                    else if (name == "IStreamQuery" && arity == 1)
                    {
                        kindLabel = "stream query";
                        expectedAttr = "StreamQueryHandler";
                    }

                    if (kindLabel is null)
                    {
                        continue;
                    }

                    // Check if any handler exists for this message type
                    string messageFQN = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!handledMessageTypes.ContainsKey(messageFQN))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.NEXUM001_NoHandlerFound,
                            type.Locations.FirstOrDefault() ?? Location.None,
                            kindLabel,
                            type.Name,
                            expectedAttr));
                    }
                }
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

        private static bool IsOpenGenericType(INamedTypeSymbol type)
        {
            return type.IsGenericType && type.IsUnboundGenericType;
        }

        private static bool HasAttribute(INamedTypeSymbol type, string attributeNamespace, string attributeName)
        {
            foreach (AttributeData attr in type.GetAttributes())
            {
                if (attr.AttributeClass is null)
                {
                    continue;
                }

                if (attr.AttributeClass.Name != attributeName)
                {
                    continue;
                }

                string? ns = GetNamespace(attr.AttributeClass.ContainingNamespace);
                if (ns == attributeNamespace)
                {
                    return true;
                }
            }
            return false;
        }

        private static string? GetNamespace(INamespaceSymbol? ns)
        {
            return ns is null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
        }
    }
}
