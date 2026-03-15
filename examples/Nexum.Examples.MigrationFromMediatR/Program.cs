using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Examples.MigrationFromMediatR.MediatR;
using Nexum.Examples.MigrationFromMediatR.Nexum;
using Nexum.Examples.MigrationFromMediatR.Shared;
using Nexum.Migration.MediatR;

// =============================================================================
// Nexum MigrationFromMediatR Example
//
// Demonstrates gradual migration from MediatR to Nexum:
//   1. Existing MediatR handlers continue working via adapter bridge
//   2. Dual-interface pattern: IRequest<T> + ICommand<T> on the same record
//   3. New functionality uses native Nexum IVoidCommand
//   4. Both dispatchers coexist — MediatR ISender and Nexum ICommandDispatcher
//
// Migration steps:
//   Step 1: Add Nexum interfaces alongside MediatR interfaces (dual-interface)
//   Step 2: Register with AddNexumWithMediatRCompat() for dual-dispatch
//   Step 3: Write new handlers natively with Nexum
//   Step 4: Gradually rewrite MediatR handlers to Nexum (handler-by-handler)
//   Step 5: Remove MediatR dependency when all handlers are migrated
// =============================================================================

Console.WriteLine("=== Nexum MigrationFromMediatR Example ===\n");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register shared in-memory store
        services.AddSingleton<CustomerStore>();

        // AddNexumWithMediatRCompat registers BOTH dispatchers:
        //   - Nexum's ICommandDispatcher, IQueryDispatcher, INotificationPublisher
        //   - MediatR's ISender, IMediator
        // MediatR IRequestHandler<T,R> is auto-bridged to Nexum via adapters.
        // MediatR IPipelineBehavior<T,R> is auto-adapted to ICommandBehavior/IQueryBehavior.
        services.AddNexumWithMediatRCompat(assemblies: typeof(Program).Assembly);
    })
    .Build();

using var scope = host.Services.CreateScope();
var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
var queries = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
var sender = scope.ServiceProvider.GetRequiredService<global::MediatR.ISender>();

// ---------------------------------------------------------------------------
// Demo 1: Dispatch MediatR command via Nexum's ICommandDispatcher
// The MediatR IRequestHandler<CreateCustomerRequest, Guid> handles it via adapter
// ---------------------------------------------------------------------------
Console.WriteLine("--- Demo 1: MediatR handler via Nexum dispatcher ---");
var createRequest = new CreateCustomerRequest("Alice", "alice@example.com");
var customerId = await commands.DispatchAsync(createRequest);
Console.WriteLine($"  Result: CustomerId = {customerId}\n");

// ---------------------------------------------------------------------------
// Demo 2: Dispatch MediatR query via Nexum's IQueryDispatcher
// The MediatR IRequestHandler<GetCustomerRequest, Customer?> handles it via adapter
// ---------------------------------------------------------------------------
Console.WriteLine("--- Demo 2: MediatR query handler via Nexum dispatcher ---");
var getRequest = new GetCustomerRequest(customerId);
var customer = await queries.DispatchAsync(getRequest);
Console.WriteLine($"  Result: {customer?.Name} ({customer?.Email})\n");

// ---------------------------------------------------------------------------
// Demo 3: Dispatch native Nexum command (new functionality)
// Uses IVoidCommand + ICommandHandler — no MediatR involvement
// ---------------------------------------------------------------------------
Console.WriteLine("--- Demo 3: Native Nexum handler (new code) ---");
await commands.DispatchAsync(new DeleteCustomerCommand(customerId));
Console.WriteLine();

// ---------------------------------------------------------------------------
// Demo 4: Dispatch via MediatR's ISender (dual-dispatch proof)
// The same CreateCustomerRequest works through MediatR's pipeline too
// ---------------------------------------------------------------------------
Console.WriteLine("--- Demo 4: Same request via MediatR ISender (dual-dispatch) ---");
var mediatRResult = await sender.Send(new CreateCustomerRequest("Bob", "bob@example.com"));
Console.WriteLine($"  Result: CustomerId = {mediatRResult}\n");

Console.WriteLine("=== Done ===");
