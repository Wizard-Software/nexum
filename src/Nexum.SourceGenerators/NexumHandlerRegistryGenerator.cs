using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Nexum.SourceGenerators
{
    [Generator]
    internal sealed class NexumHandlerRegistryGenerator : IIncrementalGenerator
    {
        // FQN constants for string-based matching
        private const string CommandHandlerAttributeFQN = "Nexum.Abstractions.CommandHandlerAttribute";
        private const string QueryHandlerAttributeFQN = "Nexum.Abstractions.QueryHandlerAttribute";
        private const string StreamQueryHandlerAttributeFQN = "Nexum.Abstractions.StreamQueryHandlerAttribute";
        private const string NotificationHandlerAttributeFQN = "Nexum.Abstractions.NotificationHandlerAttribute";
        private const string BehaviorOrderAttributeFQN = "Nexum.Abstractions.BehaviorOrderAttribute";
        private const string NexumEndpointAttributeFQN = "Nexum.Abstractions.NexumEndpointAttribute";

        // Handler interface metadata names (without namespace, with arity)
        private const string CommandHandlerInterfaceName = "ICommandHandler";
        private const string QueryHandlerInterfaceName = "IQueryHandler";
        private const string StreamQueryHandlerInterfaceName = "IStreamQueryHandler";
        private const string NotificationHandlerInterfaceName = "INotificationHandler";

        // Behavior interface metadata names (without namespace, arity is always 2)
        private const string CommandBehaviorInterfaceName = "ICommandBehavior";
        private const string QueryBehaviorInterfaceName = "IQueryBehavior";
        private const string StreamQueryBehaviorInterfaceName = "IStreamQueryBehavior";

        private const string NexumAbstractionsNamespace = "Nexum.Abstractions";

        /// <summary>
        /// SymbolDisplayFormat that uses metadata names (System.String) instead of C# keyword aliases (string).
        /// Required because emitters prepend "global::" and "global::string" is invalid C#.
        /// </summary>
        private static readonly SymbolDisplayFormat s_metadataFqnFormat =
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                & ~SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 4 branches — one per marker attribute
            IncrementalValuesProvider<HandlerRegistration?> commandHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
                CommandHandlerAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformHandler(ctx, HandlerKind.Command, CommandHandlerInterfaceName, 2, CommandHandlerAttributeFQN, ct));

            IncrementalValuesProvider<HandlerRegistration?> queryHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
                QueryHandlerAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformHandler(ctx, HandlerKind.Query, QueryHandlerInterfaceName, 2, QueryHandlerAttributeFQN, ct));

            IncrementalValuesProvider<HandlerRegistration?> streamQueryHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
                StreamQueryHandlerAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformHandler(ctx, HandlerKind.StreamQuery, StreamQueryHandlerInterfaceName, 2, StreamQueryHandlerAttributeFQN, ct));

            IncrementalValuesProvider<HandlerRegistration?> notificationHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
                NotificationHandlerAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformHandler(ctx, HandlerKind.Notification, NotificationHandlerInterfaceName, 1, NotificationHandlerAttributeFQN, ct));

            // Behavior discovery branch
            IncrementalValuesProvider<EquatableArray<BehaviorRegistration>> behaviorGroups = context.SyntaxProvider.ForAttributeWithMetadataName(
                BehaviorOrderAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformBehaviors(ctx, ct));

            // Endpoint discovery branch
            IncrementalValuesProvider<EndpointRegistration?> endpointRegistrations = context.SyntaxProvider.ForAttributeWithMetadataName(
                NexumEndpointAttributeFQN,
                predicate: static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, ct) => TransformEndpoint(ctx, ct));

            // Tier 3: Interceptor call-site discovery
            IncrementalValuesProvider<InterceptorCallSite?> interceptorCallSites = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsDispatchInvocation(node),
                    transform: static (ctx, ct) => TransformCallSite(ctx, ct));

            // Combine all 4 handler branches
            IncrementalValueProvider<EquatableArray<HandlerRegistration>> allHandlers = commandHandlers.Collect()
                .Combine(queryHandlers.Collect())
                .Combine(streamQueryHandlers.Collect())
                .Combine(notificationHandlers.Collect())
                .Select(static (combined, _) =>
                {
                    var list = new List<HandlerRegistration>();

                    // Flatten ((a, b), c), d)
                    foreach (HandlerRegistration? item in combined.Left.Left.Left)
                    {
                        if (item is not null)
                        {
                            list.Add(item);
                        }
                    }

                    foreach (HandlerRegistration? item in combined.Left.Left.Right)
                    {
                        if (item is not null)
                        {
                            list.Add(item);
                        }
                    }

                    foreach (HandlerRegistration? item in combined.Left.Right)
                    {
                        if (item is not null)
                        {
                            list.Add(item);
                        }
                    }

                    foreach (HandlerRegistration? item in combined.Right)
                    {
                        if (item is not null)
                        {
                            list.Add(item);
                        }
                    }

                    return new EquatableArray<HandlerRegistration>(list.ToArray());
                });

            // Collect all behaviors (flatten groups)
            IncrementalValueProvider<EquatableArray<BehaviorRegistration>> allBehaviors = behaviorGroups.Collect()
                .Select(static (items, _) =>
                {
                    var list = new List<BehaviorRegistration>();
                    foreach (EquatableArray<BehaviorRegistration> group in items)
                    {
                        foreach (BehaviorRegistration item in group)
                        {
                            list.Add(item);
                        }
                    }
                    return new EquatableArray<BehaviorRegistration>(list.ToArray());
                });

            // Collect all endpoints
            IncrementalValueProvider<EquatableArray<EndpointRegistration>> allEndpoints = endpointRegistrations.Collect()
                .Select(static (items, _) =>
                {
                    var list = new List<EndpointRegistration>();
                    foreach (EndpointRegistration? item in items)
                    {
                        if (item is not null)
                        {
                            list.Add(item);
                        }
                    }
                    return new EquatableArray<EndpointRegistration>(list.ToArray());
                });

            // Collect interceptor call-sites
            IncrementalValueProvider<EquatableArray<InterceptorCallSite>> allCallSites = interceptorCallSites.Collect()
                .Select(static (items, _) =>
                {
                    var list = new List<InterceptorCallSite>();
                    foreach (InterceptorCallSite? item in items)
                    {
                        if (item is not null)
                        {
                            list.Add(item.Value);
                        }
                    }
                    return new EquatableArray<InterceptorCallSite>(list.ToArray());
                });

            // Combine handlers + behaviors + endpoints + call-sites + compilation
            IncrementalValueProvider<(EquatableArray<HandlerRegistration> Left, EquatableArray<BehaviorRegistration> Right)> handlersAndBehaviors = allHandlers.Combine(allBehaviors);
            IncrementalValueProvider<((EquatableArray<HandlerRegistration> Left, EquatableArray<BehaviorRegistration> Right) Left, EquatableArray<EndpointRegistration> Right)> handlersAndBehaviorsAndEndpoints = handlersAndBehaviors.Combine(allEndpoints);
            IncrementalValueProvider<(((EquatableArray<HandlerRegistration> Left, EquatableArray<BehaviorRegistration> Right) Left, EquatableArray<EndpointRegistration> Right) Left, EquatableArray<InterceptorCallSite> Right)> handlersAndBehaviorsAndEndpointsAndCallSites = handlersAndBehaviorsAndEndpoints.Combine(allCallSites);
            IncrementalValueProvider<((((EquatableArray<HandlerRegistration> Left, EquatableArray<BehaviorRegistration> Right) Left, EquatableArray<EndpointRegistration> Right) Left, EquatableArray<InterceptorCallSite> Right) Left, Compilation Right)> combined = handlersAndBehaviorsAndEndpointsAndCallSites.Combine(context.CompilationProvider);

            context.RegisterSourceOutput(combined, static (spc, source) =>
            {
                EquatableArray<HandlerRegistration> registrations = source.Left.Left.Left.Left;
                EquatableArray<BehaviorRegistration> behaviorRegs = source.Left.Left.Left.Right;
                EquatableArray<EndpointRegistration> endpointRegs = source.Left.Left.Right;
                EquatableArray<InterceptorCallSite> callSitesList = source.Left.Right;
                Compilation compilation = source.Right;

                // Report NEXUM004 for invalid registrations
                var validRegistrations = new List<HandlerRegistration>();
                foreach (HandlerRegistration reg in registrations)
                {
                    if (reg.IsInvalid)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.NEXUM004_AttributeWithoutInterface,
                            Location.None,
                            reg.DiagnosticTypeName,
                            reg.DiagnosticAttributeName,
                            reg.DiagnosticExpectedInterface));
                    }
                    else
                    {
                        validRegistrations.Add(reg);
                    }
                }

                // Report NEXUM002 for duplicate handlers (commands/queries only, not notifications)
                IEnumerable<IGrouping<(string MessageFullyQualifiedName, HandlerKind Kind), HandlerRegistration>> grouped = validRegistrations
                    .Where(r => r.Kind != HandlerKind.Notification)
                    .GroupBy(r => (r.MessageFullyQualifiedName, r.Kind));

                foreach (IGrouping<(string MessageFullyQualifiedName, HandlerKind Kind), HandlerRegistration> group in grouped)
                {
                    var items = group.ToList();
                    if (items.Count > 1)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.NEXUM002_DuplicateHandler,
                            Location.None,
                            group.Key.Kind.ToString().ToLowerInvariant(),
                            group.Key.MessageFullyQualifiedName,
                            items[0].HandlerFullyQualifiedName,
                            items[1].HandlerFullyQualifiedName));
                    }
                }

                // Filter out duplicates from code generation (keep first)
                var deduped = new List<HandlerRegistration>();
                var seen = new HashSet<string>();
                foreach (HandlerRegistration reg in validRegistrations)
                {
                    string key = reg.Kind != HandlerKind.Notification
                        ? $"{reg.Kind}:{reg.MessageFullyQualifiedName}"
                        : $"{reg.Kind}:{reg.MessageFullyQualifiedName}:{reg.HandlerFullyQualifiedName}";
                    if (seen.Add(key))
                    {
                        deduped.Add(reg);
                    }
                }

                // Determine root namespace from assembly name
                string rootNamespace = compilation.AssemblyName ?? "Nexum.Generated";

                // Emit handler registry only if there are handlers
                if (deduped.Count > 0)
                {
                    string sourceCode = NexumHandlerRegistryEmitter.Emit(
                        rootNamespace,
                        new EquatableArray<HandlerRegistration>(deduped.ToArray()));

                    spc.AddSource("NexumHandlerRegistry.g.cs", sourceCode);
                }

                // Emit pipeline registry (Tier 2) - only if there are behaviors with [BehaviorOrder]
                if (behaviorRegs.Length > 0)
                {
                    string pipelineSource = NexumPipelineRegistryEmitter.Emit(
                        rootNamespace,
                        new EquatableArray<HandlerRegistration>(deduped.ToArray()),
                        behaviorRegs);
                    spc.AddSource("NexumPipelineRegistry.g.cs", pipelineSource);
                }

                // Process endpoints
                if (endpointRegs.Length > 0)
                {
                    // Check if ASP.NET Core is available in compilation
                    bool hasAspNetCore = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Routing.IEndpointRouteBuilder") is not null;

                    if (hasAspNetCore)
                    {
                        // Validate endpoints and report diagnostics
                        var validEndpoints = new List<EndpointRegistration>();
                        foreach (EndpointRegistration ep in endpointRegs)
                        {
                            if (ep.IsInvalid)
                            {
                                spc.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.NEXUM008_EndpointOnNonMessageType,
                                    Location.None,
                                    ep.DiagnosticTypeName));
                            }
                            else
                            {
                                validEndpoints.Add(ep);
                            }
                        }

                        // Check for duplicate routes
                        IEnumerable<IGrouping<(string HttpMethod, string Pattern), EndpointRegistration>> routeGroups = validEndpoints
                            .GroupBy(e => (e.HttpMethod, e.Pattern));

                        foreach (IGrouping<(string HttpMethod, string Pattern), EndpointRegistration> group in routeGroups)
                        {
                            var items = group.ToList();
                            if (items.Count > 1)
                            {
                                spc.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.NEXUM009_DuplicateEndpointRoute,
                                    Location.None,
                                    group.Key.HttpMethod,
                                    group.Key.Pattern,
                                    items[0].MessageFullyQualifiedName,
                                    items[1].MessageFullyQualifiedName));
                            }
                        }

                        // Emit endpoint registration (filter out duplicates)
                        var dedupedEndpoints = new List<EndpointRegistration>();
                        var seenRoutes = new HashSet<(string, string)>();
                        foreach (EndpointRegistration ep in validEndpoints)
                        {
                            if (seenRoutes.Add((ep.HttpMethod, ep.Pattern)))
                            {
                                dedupedEndpoints.Add(ep);
                            }
                        }

                        if (dedupedEndpoints.Count > 0)
                        {
                            string endpointSource = NexumEndpointEmitter.Emit(
                                rootNamespace,
                                new EquatableArray<EndpointRegistration>(dedupedEndpoints.ToArray()));
                            spc.AddSource("NexumEndpointRegistration.g.cs", endpointSource);
                        }
                    }
                    // If ASP.NET Core is not available, silently skip (no diagnostics)
                }

                // Tier 3: Emit interceptors
                if (callSitesList.Length > 0)
                {
                    // Build known handler message types set for filtering
                    var knownHandlerMessages = new HashSet<string>();
                    foreach (HandlerRegistration reg in deduped)
                    {
                        if (reg.Kind != HandlerKind.Notification)
                        {
                            knownHandlerMessages.Add(reg.MessageFullyQualifiedName);
                        }
                    }

                    // Separate interceptable call-sites from skipped ones
                    var interceptableCallSites = new List<InterceptorCallSite>();
                    foreach (InterceptorCallSite cs in callSitesList)
                    {
                        // Skip call-sites marked with SkipReason (e.g., non-concrete type)
                        if (cs.SkipReason is not null)
                        {
                            // Emit NEXUM006 for non-concrete type call-sites
                            spc.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.NEXUM006_CannotInterceptDispatch,
                                Location.None,
                                cs.MessageFullyQualifiedName));
                            continue;
                        }

                        // Skip call-sites where handler is not in current compilation
                        if (!knownHandlerMessages.Contains(cs.MessageFullyQualifiedName))
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.NEXUM007_InterceptorSkippedNoHandler,
                                Location.None,
                                cs.MessageFullyQualifiedName,
                                cs.Kind.ToString().ToLowerInvariant()));
                            continue;
                        }

                        interceptableCallSites.Add(cs);
                    }

                    // Emit NEXUM005 and interceptor source only for interceptable call-sites
                    if (interceptableCallSites.Count > 0)
                    {
                        foreach (InterceptorCallSite cs in interceptableCallSites)
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                DiagnosticDescriptors.NEXUM005_InterceptorGenerated,
                                Location.None,
                                cs.Kind.ToString().ToLowerInvariant(),
                                cs.MessageFullyQualifiedName,
                                "generated",
                                "0"));
                        }

                        string interceptorSource = NexumInterceptorEmitter.Emit(
                            rootNamespace,
                            new EquatableArray<InterceptorCallSite>(interceptableCallSites.ToArray()),
                            new EquatableArray<HandlerRegistration>(deduped.ToArray()),
                            behaviorRegs.Length > 0);
                        spc.AddSource("NexumInterceptors.g.cs", interceptorSource);
                    }
                }
            });
        }

        private static HandlerRegistration? TransformHandler(
            GeneratorAttributeSyntaxContext context,
            HandlerKind kind,
            string expectedInterfaceName,
            int expectedArity,
            string attributeFQN,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var symbol = (INamedTypeSymbol)context.TargetSymbol;
            string handlerFQN = GetFullyQualifiedName(symbol);

            // Find the matching handler interface
            foreach (INamedTypeSymbol iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (iface.Arity != expectedArity)
                {
                    continue;
                }

                if (iface.Name != expectedInterfaceName)
                {
                    continue;
                }

                // Check namespace via ContainingNamespace
                string? ns = GetNamespaceName(iface.ContainingNamespace);
                if (ns != NexumAbstractionsNamespace)
                {
                    continue;
                }

                // Found matching interface — extract type arguments
                string serviceFQN = GetFullyQualifiedName(iface);
                string messageFQN = GetFullyQualifiedName((INamedTypeSymbol)iface.TypeArguments[0]);
                string? resultFQN = expectedArity >= 2
                    ? GetFullyQualifiedName((INamedTypeSymbol)iface.TypeArguments[1])
                    : null;

                // Extract [HandlerLifetime] if present
                string? lifetime = ExtractLifetime(symbol);

                return new HandlerRegistration(
                    HandlerFullyQualifiedName: handlerFQN,
                    ServiceInterfaceFullyQualifiedName: serviceFQN,
                    MessageFullyQualifiedName: messageFQN,
                    ResultFullyQualifiedName: resultFQN,
                    Kind: kind,
                    Lifetime: lifetime);
            }

            // Has attribute but no matching interface — NEXUM004
            // Compute friendly attribute name (strip "Attribute" suffix and namespace)
            string attrShortName = attributeFQN;
            int lastDot = attributeFQN.LastIndexOf('.');
            if (lastDot >= 0)
            {
                attrShortName = attributeFQN.Substring(lastDot + 1);
            }

            if (attrShortName.EndsWith("Attribute"))
            {
                attrShortName = attrShortName.Substring(0, attrShortName.Length - "Attribute".Length);
            }

            string expectedInterface = $"Nexum.Abstractions.{expectedInterfaceName}<{new string(',', expectedArity - 1)}>";

            return new HandlerRegistration(
                HandlerFullyQualifiedName: handlerFQN,
                ServiceInterfaceFullyQualifiedName: "",
                MessageFullyQualifiedName: "",
                ResultFullyQualifiedName: null,
                Kind: kind,
                Lifetime: null,
                IsInvalid: true,
                DiagnosticTypeName: handlerFQN,
                DiagnosticAttributeName: attrShortName,
                DiagnosticExpectedInterface: expectedInterface);
        }

        private static string? ExtractLifetime(INamedTypeSymbol symbol)
        {
            foreach (AttributeData attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass is null)
                {
                    continue;
                }

                string? attrNs = GetNamespaceName(attr.AttributeClass.ContainingNamespace);
                if (attrNs != NexumAbstractionsNamespace || attr.AttributeClass.Name != "HandlerLifetimeAttribute")
                {
                    continue;
                }

                // HandlerLifetimeAttribute has one constructor arg: NexumLifetime enum value
                if (attr.ConstructorArguments.Length > 0)
                {
                    TypedConstant arg = attr.ConstructorArguments[0];
                    if (arg.Value is int enumValue)
                    {
                        // NexumLifetime: Transient=0, Scoped=1, Singleton=2
                        return enumValue switch
                        {
                            0 => "Transient",
                            1 => "Scoped",
                            2 => "Singleton",
                            _ => null
                        };
                    }
                }

                break;
            }

            return null;
        }

        private static EquatableArray<BehaviorRegistration> TransformBehaviors(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var symbol = (INamedTypeSymbol)context.TargetSymbol;
            string behaviorFQN = GetFullyQualifiedName(symbol);

            // Extract order from [BehaviorOrder(int)] attribute
            int order = 0;
            foreach (AttributeData attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass is null)
                {
                    continue;
                }

                string? attrNs = GetNamespaceName(attr.AttributeClass.ContainingNamespace);
                if (attrNs == NexumAbstractionsNamespace && attr.AttributeClass.Name == "BehaviorOrderAttribute"
                    && attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is int orderValue)
                {
                    order = orderValue;
                    break;
                }
            }

            // Search all interfaces for behavior interfaces
            var results = new List<BehaviorRegistration>();
            foreach (INamedTypeSymbol iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();

                if (iface.Arity != 2)
                {
                    continue;
                }

                string? ns = GetNamespaceName(iface.ContainingNamespace);
                if (ns != NexumAbstractionsNamespace)
                {
                    continue;
                }

                BehaviorKind? kind = iface.Name switch
                {
                    CommandBehaviorInterfaceName => BehaviorKind.Command,
                    QueryBehaviorInterfaceName => BehaviorKind.Query,
                    StreamQueryBehaviorInterfaceName => BehaviorKind.StreamQuery,
                    _ => null
                };

                if (kind is null)
                {
                    continue;
                }

                // Determine if behavior is open generic
                // A behavior class like `LoggingBehavior<TCmd, TRes> : ICommandBehavior<TCmd, TRes>` is open generic
                // A behavior class like `CreateOrderValidation : ICommandBehavior<CreateOrderCommand, OrderId>` is closed
                bool isOpenGeneric = symbol.TypeParameters.Length > 0;

                string? messageFQN = null;
                string? resultFQN = null;
                string serviceFQN;

                if (isOpenGeneric)
                {
                    // Open generic — we store the open generic interface FQN
                    // e.g., ICommandBehavior<,> for the behavior itself
                    serviceFQN = GetFullyQualifiedName(iface);
                }
                else
                {
                    // Closed generic — extract concrete type arguments
                    messageFQN = GetFullyQualifiedName((INamedTypeSymbol)iface.TypeArguments[0]);
                    resultFQN = GetFullyQualifiedName((INamedTypeSymbol)iface.TypeArguments[1]);
                    serviceFQN = GetFullyQualifiedName(iface);
                }

                results.Add(new BehaviorRegistration(
                    BehaviorFullyQualifiedName: behaviorFQN,
                    Kind: kind.Value,
                    Order: order,
                    IsOpenGeneric: isOpenGeneric,
                    MessageFullyQualifiedName: messageFQN,
                    ResultFullyQualifiedName: resultFQN,
                    ServiceInterfaceFullyQualifiedName: serviceFQN));
            }

            // Return as EquatableArray (may be empty if no behavior interfaces found)
            return new EquatableArray<BehaviorRegistration>(results.ToArray());
        }

        private static EndpointRegistration? TransformEndpoint(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var symbol = (INamedTypeSymbol)context.TargetSymbol;
            string typeFQN = GetFullyQualifiedName(symbol);

            // Extract [NexumEndpoint] attribute data
            string? httpMethod = null;
            string? pattern = null;
            string? name = null;
            string? groupName = null;

            foreach (AttributeData attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass is null)
                {
                    continue;
                }

                string? attrNs = GetNamespaceName(attr.AttributeClass.ContainingNamespace);
                if (attrNs != NexumAbstractionsNamespace || attr.AttributeClass.Name != "NexumEndpointAttribute")
                {
                    continue;
                }

                // Constructor args: (NexumHttpMethod method, string pattern)
                if (attr.ConstructorArguments.Length >= 2)
                {
                    if (attr.ConstructorArguments[0].Value is int methodValue)
                    {
                        httpMethod = methodValue switch
                        {
                            0 => "Get",
                            1 => "Post",
                            2 => "Put",
                            3 => "Delete",
                            4 => "Patch",
                            _ => "Post"
                        };
                    }
                    pattern = attr.ConstructorArguments[1].Value as string;
                }

                // Named args: Name, GroupName
                foreach (System.Collections.Generic.KeyValuePair<string, TypedConstant> namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Name")
                    {
                        name = namedArg.Value.Value as string;
                    }
                    if (namedArg.Key == "GroupName")
                    {
                        groupName = namedArg.Value.Value as string;
                    }
                }
                break;
            }

            if (httpMethod is null || pattern is null)
            {
                return null;
            }

            // Determine if type implements ICommand<T> or IQuery<T>
            HandlerKind? kind = null;
            string? resultFQN = null;
            ITypeSymbol? resultTypeSymbol = null;

            foreach (INamedTypeSymbol iface in symbol.AllInterfaces)
            {
                ct.ThrowIfCancellationRequested();
                string? ns = GetNamespaceName(iface.ContainingNamespace);
                if (ns != NexumAbstractionsNamespace)
                {
                    continue;
                }

                // Check for IVoidCommand first (it extends ICommand<Unit>)
                if (iface.Name == "IVoidCommand" && iface.Arity == 0)
                {
                    kind = HandlerKind.Command;
                    resultFQN = "Nexum.Abstractions.Unit";
                    break;
                }

                if (iface.Name == "ICommand" && iface.Arity == 1)
                {
                    kind = HandlerKind.Command;
                    resultTypeSymbol = iface.TypeArguments[0];
                    resultFQN = GetFullyQualifiedName(resultTypeSymbol);
                    break;
                }
                if (iface.Name == "IQuery" && iface.Arity == 1)
                {
                    kind = HandlerKind.Query;
                    resultTypeSymbol = iface.TypeArguments[0];
                    resultFQN = GetFullyQualifiedName(resultTypeSymbol);
                    break;
                }
            }

            if (kind is null)
            {
                // NEXUM008: [NexumEndpoint] on non-command/query type
                return new EndpointRegistration(
                    MessageFullyQualifiedName: typeFQN,
                    ResultFullyQualifiedName: null,
                    HttpMethod: httpMethod,
                    Pattern: pattern,
                    Name: name,
                    GroupName: groupName,
                    Kind: HandlerKind.Command, // placeholder
                    HasResultMembers: false,
                    IsInvalid: true,
                    DiagnosticTypeName: typeFQN);
            }

            // Check if result type has structural Result members (duck typing)
            bool hasResultMembers = false;
            if (resultTypeSymbol is not null && resultFQN != "Nexum.Abstractions.Unit")
            {
                // Look for IsSuccess (bool), Value, and Error properties
                bool hasIsSuccess = false;
                bool hasValue = false;
                bool hasError = false;

                foreach (ISymbol member in resultTypeSymbol.GetMembers())
                {
                    if (member is IPropertySymbol prop)
                    {
                        if (prop.Name == "IsSuccess" && prop.Type.SpecialType == SpecialType.System_Boolean)
                        {
                            hasIsSuccess = true;
                        }
                        else if (prop.Name == "Value")
                        {
                            hasValue = true;
                        }
                        else if (prop.Name == "Error")
                        {
                            hasError = true;
                        }
                    }
                }

                hasResultMembers = hasIsSuccess && hasValue && hasError;
            }

            return new EndpointRegistration(
                MessageFullyQualifiedName: typeFQN,
                ResultFullyQualifiedName: resultFQN,
                HttpMethod: httpMethod,
                Pattern: pattern,
                Name: name,
                GroupName: groupName,
                Kind: kind.Value,
                HasResultMembers: hasResultMembers);
        }

        private static string GetFullyQualifiedName(INamedTypeSymbol symbol)
        {
            // Use s_metadataFqnFormat to avoid C# keyword aliases (string → System.String)
            return symbol.ToDisplayString(s_metadataFqnFormat)
                .Replace("global::", "");
        }

        private static string GetFullyQualifiedName(ITypeSymbol symbol)
        {
            return symbol.ToDisplayString(s_metadataFqnFormat)
                .Replace("global::", "");
        }

        private static string? GetNamespaceName(INamespaceSymbol? ns)
        {
            return ns is null || ns.IsGlobalNamespace ? null : ns.ToDisplayString();
        }

        /// <summary>
        /// Fast syntactic check: is this an invocation of DispatchAsync or StreamAsync?
        /// </summary>
        private static bool IsDispatchInvocation(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax invocation)
            {
                return false;
            }

            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return false;
            }

            string name = memberAccess.Name.Identifier.Text;
            return name is "DispatchAsync" or "StreamAsync";
        }

        private static InterceptorCallSite? TransformCallSite(
            GeneratorSyntaxContext ctx,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var invocation = (InvocationExpressionSyntax)ctx.Node;
            SemanticModel model = ctx.SemanticModel;

            // Resolve method symbol
            SymbolInfo symbolInfo = model.GetSymbolInfo(invocation, ct);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return null;
            }

            // Check if method is on a Nexum dispatcher interface
            INamedTypeSymbol? containingType = methodSymbol.ContainingType;
            if (containingType is null)
            {
                return null;
            }

            string? containingTypeFQN = GetFullyQualifiedName(containingType);

            InterceptorKind? kind = null;
            if (containingTypeFQN == "Nexum.Abstractions.ICommandDispatcher" && methodSymbol.Name == "DispatchAsync")
            {
                kind = InterceptorKind.Command;
            }
            else if (containingTypeFQN == "Nexum.Abstractions.IQueryDispatcher" && methodSymbol.Name == "DispatchAsync")
            {
                kind = InterceptorKind.Query;
            }
            else if (containingTypeFQN == "Nexum.Abstractions.IQueryDispatcher" && methodSymbol.Name == "StreamAsync")
            {
                kind = InterceptorKind.StreamQuery;
            }

            if (kind is null)
            {
                return null;
            }

            // Extract TResult from method's type arguments
            if (methodSymbol.TypeArguments.Length != 1)
            {
                return null;
            }

            ITypeSymbol resultType = methodSymbol.TypeArguments[0];
            string resultFQN = GetFullyQualifiedName(resultType);

            // Extract concrete command/query type from first argument
            if (invocation.ArgumentList.Arguments.Count < 1)
            {
                return null;
            }

            ExpressionSyntax firstArgExpr = invocation.ArgumentList.Arguments[0].Expression;
            TypeInfo typeInfo = model.GetTypeInfo(firstArgExpr, ct);
            ITypeSymbol? concreteType = typeInfo.Type;

            // If we can't determine the concrete type, we can't intercept
            if (concreteType is null || concreteType.TypeKind == TypeKind.Error)
            {
                return null;
            }

            // Get InterceptableLocation from Roslyn API
            InterceptableLocation? location = model.GetInterceptableLocation(invocation, ct);
            if (location is null)
            {
                return null;
            }

            string interceptsLocationSyntax = location.GetInterceptsLocationAttributeSyntax();

            // Check if the concrete type is actually concrete (not an interface/abstract)
            // For interceptors, we need the exact runtime type
            if (concreteType is INamedTypeSymbol namedConcreteType)
            {
                if (namedConcreteType.IsAbstract || concreteType.TypeKind == TypeKind.Interface)
                {
                    // Return with SkipReason so NEXUM006 can be emitted in RegisterSourceOutput
                    string messageFQN = GetFullyQualifiedName(concreteType);
                    return new InterceptorCallSite(
                        Kind: kind.Value,
                        MessageFullyQualifiedName: messageFQN,
                        ResultFullyQualifiedName: resultFQN,
                        InterceptsLocationSyntax: interceptsLocationSyntax,
                        DispatcherInterfaceFullyQualifiedName: containingTypeFQN,
                        SkipReason: "NonConcreteType");
                }
            }
            else
            {
                // Type parameter or other non-concrete type
                string messageFQN = GetFullyQualifiedName(concreteType);
                return new InterceptorCallSite(
                    Kind: kind.Value,
                    MessageFullyQualifiedName: messageFQN,
                    ResultFullyQualifiedName: resultFQN,
                    InterceptsLocationSyntax: interceptsLocationSyntax,
                    DispatcherInterfaceFullyQualifiedName: containingTypeFQN,
                    SkipReason: "NonConcreteType");
            }

            string concreteMessageFQN = GetFullyQualifiedName(concreteType);

            return new InterceptorCallSite(
                Kind: kind.Value,
                MessageFullyQualifiedName: concreteMessageFQN,
                ResultFullyQualifiedName: resultFQN,
                InterceptsLocationSyntax: interceptsLocationSyntax,
                DispatcherInterfaceFullyQualifiedName: containingTypeFQN);
        }
    }
}
