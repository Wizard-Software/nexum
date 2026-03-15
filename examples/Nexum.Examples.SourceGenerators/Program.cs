using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Examples.SourceGenerators.Commands;
using Nexum.Examples.SourceGenerators.Notifications;
using Nexum.Examples.SourceGenerators.Queries;
using Nexum.Examples.SourceGenerators.Singletons;
using Nexum.Examples.SourceGenerators.Behaviors;

Console.WriteLine("=== Nexum SourceGenerators Example ===");
Console.WriteLine();
Console.WriteLine("This example demonstrates Nexum.SourceGenerators Tier 1-3:");
Console.WriteLine("  Tier 1 — DI registration via NexumHandlerRegistry (no reflection at startup)");
Console.WriteLine("  Tier 2 — Compiled pipeline delegates via NexumPipelineRegistry (no boxing on hot path)");
Console.WriteLine("  Tier 3 — Call-site interceptors via [InterceptsLocation] (transparent dispatch acceleration)");
Console.WriteLine();

// ─── Host setup ────────────────────────────────────────────────────────────────
//
// SG Tier 1: When Nexum.SourceGenerators is referenced as an Analyzer, it emits
//            NexumHandlerRegistry — a static class with an AddNexumHandlers() method
//            containing explicit ServiceDescriptor registrations for every type annotated
//            with [CommandHandler], [QueryHandler], [NotificationHandler], etc.
//
//            AddNexum() detects NexumHandlerRegistry via name lookup and calls
//            AddNexumHandlers() — zero reflection on handler types at runtime.
//
//            Check: obj/Debug/net10.0/generated/Nexum.SourceGenerators/
//                            Nexum.SourceGenerators.NexumHandlerRegistryGenerator/
//                            NexumHandlerRegistry.g.cs
//
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        AddNexumServices(services);

        // Behaviors must be registered explicitly — AddNexum() scans handlers only.
        // SG Tier 2: These open-generic behaviors are inlined into compiled pipeline
        //            delegates in NexumPipelineRegistry — one monomorphized method per
        //            (TCommand, TResult) pair, eliding all virtual dispatch.
        services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>));
        services.AddNexumBehavior(typeof(TimingCommandBehavior<,>));

        // SG Tier 2 — concrete type registration:
        // The generated NexumPipelineRegistry calls GetRequiredService<ConcreteType>() directly
        // (e.g. GetRequiredService<CreateInvoiceHandler>(), GetRequiredService<LoggingCommandBehavior<...>>()).
        // NexumHandlerRegistry registers handlers under their service interfaces (ICommandHandler<T,R>),
        // so we also register the concrete types to satisfy the compiled pipeline's direct DI lookups.
        services.AddScoped<CreateInvoiceHandler>();
        services.AddTransient<LoggingCommandBehavior<CreateInvoiceCommand, Guid>>();
        services.AddTransient<TimingCommandBehavior<CreateInvoiceCommand, Guid>>();
        services.AddScoped<GetInvoiceHandler>();
        services.AddSingleton<CachedQueryHandler>();
    })
    .Build();

// ─── Section 1: Tier 1 — SG-generated DI registration ─────────────────────────
Console.WriteLine("--- Tier 1: SG-generated DI Registration ---");
Console.WriteLine("  NexumHandlerRegistry.AddNexumHandlers() registered all handlers without reflection.");
Console.WriteLine();

using var scope = host.Services.CreateScope();
var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
var queries = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
var notifications = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

// ─── Section 2: Tier 2 — Compiled pipeline (behaviors inlined by SG) ───────────
//
// SG Tier 2: The Source Generator emits NexumPipelineRegistry with a method like:
//
//   public static ValueTask<Guid> Dispatch_CreateInvoiceCommand(
//       CreateInvoiceCommand command,
//       ICommandHandler<CreateInvoiceCommand, Guid> handler,
//       LoggingCommandBehavior<CreateInvoiceCommand, Guid> logging,
//       TimingCommandBehavior<CreateInvoiceCommand, Guid> timing,
//       CancellationToken ct) => ...
//
//   The behavior chain is resolved at compile time — no MakeGenericType, no reflection.
//
Console.WriteLine("--- Tier 2: Compiled Pipeline (behaviors inlined by SG) ---");
Console.WriteLine("  Pipeline order: LoggingBehavior(1) → TimingBehavior(2) → CreateInvoiceHandler");
Console.WriteLine();

// SG Tier 3: The call-site below is intercepted at compile time.
//            The Source Generator emits an [InterceptsLocation] interceptor that replaces
//            this exact call to dispatcher.DispatchAsync() with a direct call to the
//            compiled pipeline delegate in NexumPipelineRegistry — eliminating all
//            runtime type resolution overhead.
//
//            Check: obj/Debug/net10.0/generated/Nexum.SourceGenerators/
//                            Nexum.SourceGenerators.NexumHandlerRegistryGenerator/
//                            NexumInterceptors.g.cs
//
Console.WriteLine("--- Tier 3: Call-site Interceptors ---");
Console.WriteLine("  SG generates [InterceptsLocation] interceptors on DispatchAsync() call-sites.");
Console.WriteLine("  dispatcher.DispatchAsync(cmd) → NexumInterceptors.Intercept_DispatchAsync_0001(...)");
Console.WriteLine();

var invoiceId = await commands.DispatchAsync(new CreateInvoiceCommand("Acme Corp", 1_250.00m));
Console.WriteLine($"  Created invoice: {invoiceId}");
Console.WriteLine();

// ─── Section 3: Query dispatch ─────────────────────────────────────────────────
Console.WriteLine("--- Query Dispatch ---");
var invoice = await queries.DispatchAsync(new GetInvoiceQuery(invoiceId));
Console.WriteLine($"  Retrieved: {invoice.Customer} {invoice.Amount:C}");
Console.WriteLine();

// ─── Section 4: Notification publish ──────────────────────────────────────────
Console.WriteLine("--- Notification Publish ---");
await notifications.PublishAsync(new InvoiceCreatedNotification(invoiceId, "Acme Corp", 1_250.00m));
Console.WriteLine();

// ─── Section 5: [HandlerLifetime] — Singleton override ────────────────────────
//
// [HandlerLifetime(NexumLifetime.Singleton)] — SG registers CachedQueryHandler
//            as Singleton instead of the default Scoped. Dispatching from two
//            different scopes returns the same handler instance.
//
Console.WriteLine("--- [HandlerLifetime(Singleton)] Override ---");
Console.WriteLine("  CachedQueryHandler is registered as Singleton — same instance across all scopes.");
Console.WriteLine();

// First scope
using (var scope1 = host.Services.CreateScope())
{
    var q1 = scope1.ServiceProvider.GetRequiredService<IQueryDispatcher>();
    var rate1 = await q1.DispatchAsync(new GetExchangeRateQuery("EUR"));
    Console.WriteLine($"  Scope 1 → EUR rate: {rate1}");
}

// Second scope — same Singleton instance handles the query
using (var scope2 = host.Services.CreateScope())
{
    var q2 = scope2.ServiceProvider.GetRequiredService<IQueryDispatcher>();
    var rate2 = await q2.DispatchAsync(new GetExchangeRateQuery("GBP"));
    Console.WriteLine($"  Scope 2 → GBP rate: {rate2}");
}

Console.WriteLine();

// ─── Section 6: Marker attributes summary ─────────────────────────────────────
Console.WriteLine("--- Source Generator Marker Attributes ---");
Console.WriteLine("  [CommandHandler]      → handler discovered for ICommandHandler<T,R>");
Console.WriteLine("  [QueryHandler]        → handler discovered for IQueryHandler<T,R>");
Console.WriteLine("  [NotificationHandler] → handler discovered for INotificationHandler<T>");
Console.WriteLine("  [BehaviorOrder(n)]    → pipeline position (lower = outermost)");
Console.WriteLine("  [HandlerLifetime(..)] → override default Scoped lifetime per handler");
Console.WriteLine();
Console.WriteLine("  NEXUM003 diagnostic: SG emits a warning when a handler class is missing");
Console.WriteLine("  its marker attribute — caught at compile time, not runtime.");
Console.WriteLine();

Console.WriteLine("=== Done ===");

[RequiresUnreferencedCode("Assembly scanning uses reflection. Use Nexum.SourceGenerators for AOT-safe registration.")]
[RequiresDynamicCode("Assembly scanning uses MakeGenericType. Use Nexum.SourceGenerators for AOT-safe registration.")]
static void AddNexumServices(IServiceCollection services)
{
    // When SG is active, AddNexum() finds NexumHandlerRegistry and calls AddNexumHandlers()
    // — the reflection-based ScanAndRegisterHandlers path is NOT taken.
    // The [RequiresUnreferencedCode] attributes above document the fallback path only.
    services.AddNexum(assemblies: typeof(Program).Assembly);
}
