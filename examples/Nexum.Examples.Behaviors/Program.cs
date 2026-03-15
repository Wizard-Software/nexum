using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Examples.Behaviors.Commands;
using Nexum.Examples.Behaviors.Queries;
using Nexum.Examples.Behaviors.Notifications;
using Nexum.Examples.Behaviors.Behaviors;
using Nexum.Examples.Behaviors.ExceptionHandlers;

Console.WriteLine("=== Nexum Behaviors Example ===\n");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        AddNexumServices(services);

        // Behaviors require explicit registration (AddNexum only scans handlers)
        services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>));
        services.AddNexumBehavior(typeof(CachingQueryBehavior<,>));
        services.AddNexumBehavior(typeof(FilteringStreamBehavior<,>));

        // Exception handlers also require explicit registration
        services.AddNexumExceptionHandler<OrderCommandExceptionHandler>();
        services.AddNexumExceptionHandler<OrderNotificationExceptionHandler>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
var queries = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
var notifications = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

// 1. Command with LoggingBehavior — Russian doll visualization
Console.WriteLine("--- 1. Command with LoggingBehavior ---");
var orderId = await commands.DispatchAsync(new PlaceOrderCommand("Widget", 5));
Console.WriteLine($"  Order result: {orderId}");

// 2. Query with CachingBehavior — 2 calls, second from cache
Console.WriteLine("\n--- 2. Query with CachingBehavior ---");
Console.WriteLine("  First call:");
var price1 = await queries.DispatchAsync(new GetProductQuery("Widget"));
Console.WriteLine($"  Price: {price1:C}");

Console.WriteLine("  Second call (should be cached):");
var price2 = await queries.DispatchAsync(new GetProductQuery("Widget"));
Console.WriteLine($"  Price: {price2:C}");

// 3. Stream with FilteringBehavior
Console.WriteLine("\n--- 3. Stream with FilteringBehavior ---");
await foreach (var product in queries.StreamAsync(new ListProductsStreamQuery(10.0m)))
{
    Console.WriteLine($"  Received: {product}");
}

// 4. Command exception handler — invalid quantity triggers exception
Console.WriteLine("\n--- 4. Command Exception Handler ---");
try
{
    await commands.DispatchAsync(new PlaceOrderCommand("Widget", -1));
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Exception propagated (as expected): {ex.Message}");
}

// 5. Notification exception handler
Console.WriteLine("\n--- 5. Notification Exception Handler ---");
try
{
    await notifications.PublishAsync(new OrderPlacedNotification("ORD-TEST-001"));
}
catch (Exception ex)
{
    Console.WriteLine($"  Exception propagated (as expected): {ex.Message}");
}

Console.WriteLine("\n=== Done ===");

[RequiresUnreferencedCode("Assembly scanning uses reflection. Use Nexum.SourceGenerators for AOT-safe registration.")]
[RequiresDynamicCode("Assembly scanning uses MakeGenericType. Use Nexum.SourceGenerators for AOT-safe registration.")]
static void AddNexumServices(IServiceCollection services)
{
    services.AddNexum(assemblies: typeof(Program).Assembly);
}
