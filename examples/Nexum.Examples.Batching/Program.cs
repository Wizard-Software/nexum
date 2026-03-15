using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Batching;
using Nexum.Extensions.DependencyInjection;
using Nexum.Examples.Batching.Queries;

Console.WriteLine("=== Nexum Batching Example ===\n");

// Build and configure the host
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Step 1: Register Nexum dispatchers and handlers via assembly scanning
        services.AddNexum(assemblies: typeof(Program).Assembly);

        // Step 2: Register batching AFTER AddNexum() — it decorates IQueryDispatcher
        // with a BatchingQueryDispatcher that accumulates concurrent queries
        // within the BatchWindow before dispatching them as a single batch.
        services.AddNexumBatching(
            opts =>
            {
                // Queries arriving within 50 ms of the first one will be batched together
                opts.BatchWindow = TimeSpan.FromMilliseconds(50);
                // Maximum number of queries in a single batch
                opts.MaxBatchSize = 20;
            },
            typeof(Program).Assembly);
    })
    .Build();

using var scope = host.Services.CreateScope();
var queries = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

// Step 3: Dispatch 10 queries concurrently for products with IDs 1–10
// Because they all start within the 50 ms BatchWindow, the batching layer
// collects all 10 and delivers them to GetProductByIdBatchHandler as ONE call.
Console.WriteLine("--- Dispatching 10 concurrent GetProductByIdQuery requests ---\n");
Console.WriteLine("  (All queries start within the 50 ms BatchWindow — expect 1 batch)\n");

var tasks = Enumerable.Range(1, 10)
    .Select(id => queries.DispatchAsync(new GetProductByIdQuery(id)).AsTask())
    .ToArray();

var products = await Task.WhenAll(tasks);

// Step 4: Print results showing that all 10 resolved via a single batch invocation
Console.WriteLine("\n--- Results ---\n");
foreach (var product in products)
{
    Console.WriteLine($"  Product #{product.Id}: {product.Name} — ${product.Price:F2}");
}

Console.WriteLine("\n=== Done ===");
