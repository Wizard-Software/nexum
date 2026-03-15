using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexum.Abstractions;
using Nexum.Examples.Notifications.ExceptionHandlers;
using Nexum.Examples.Notifications.Notifications;
using Nexum.Extensions.DependencyInjection;

Console.WriteLine("=== Nexum Notifications Example ===");
Console.WriteLine("Demonstrating 4 PublishStrategy variants\n");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.ClearProviders())
    .ConfigureServices(services =>
    {
        services.AddNexum(assemblies: typeof(Program).Assembly);
        services.AddNexumExceptionHandler<PriceChangedExceptionHandler>();
    })
    .Build();

// Must start host for BackgroundService (FireAndForget)
await host.StartAsync();

using var scope = host.Services.CreateScope();
var publisher = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

// --- 1. Sequential ---
Console.WriteLine("--- 1. Sequential ---");
Console.WriteLine("Handlers execute one after another, in registration order.\n");

var notification = new PriceChangedNotification("Widget", 10.00m, 12.50m);
var sw = Stopwatch.StartNew();
await publisher.PublishAsync(notification, PublishStrategy.Sequential);
sw.Stop();
Console.WriteLine($"\n  Completed in {sw.ElapsedMilliseconds}ms\n");

// --- 2. Parallel ---
Console.WriteLine("--- 2. Parallel ---");
Console.WriteLine("Handlers execute concurrently — significantly faster.\n");

sw.Restart();
await publisher.PublishAsync(
    new PriceChangedNotification("Gadget", 25.00m, 22.00m),
    PublishStrategy.Parallel);
sw.Stop();
Console.WriteLine($"\n  Completed in {sw.ElapsedMilliseconds}ms (should be ~max single handler time)\n");

// --- 3. StopOnException ---
Console.WriteLine("--- 3. StopOnException ---");
Console.WriteLine("Stops at first exception — remaining handlers do NOT execute.\n");

try
{
    // Negative price triggers exception in SMS handler; Slow handler will NOT run
    await publisher.PublishAsync(
        new PriceChangedNotification("Broken", 5.00m, -1.00m),
        PublishStrategy.StopOnException);
}
catch (Exception ex)
{
    Console.WriteLine($"\n  Exception caught: {ex.Message}");
    Console.WriteLine("  (Notice: handlers after the failing one did NOT execute)\n");
}

// --- 4. FireAndForget ---
Console.WriteLine("--- 4. FireAndForget ---");
Console.WriteLine("PublishAsync returns immediately. Handlers run in background.\n");

sw.Restart();
await publisher.PublishAsync(
    new PriceChangedNotification("AsyncItem", 100.00m, 95.00m),
    PublishStrategy.FireAndForget);
sw.Stop();
Console.WriteLine($"  PublishAsync returned in {sw.ElapsedMilliseconds}ms (near-zero)");
Console.WriteLine("  Waiting for background processing...\n");
await Task.Delay(2000);

Console.WriteLine("  Now testing FireAndForget with exception...");
await publisher.PublishAsync(
    new PriceChangedNotification("ErrorItem", 5.00m, -1.00m),
    PublishStrategy.FireAndForget);
Console.WriteLine("  PublishAsync returned (exception will be handled in background)");
await Task.Delay(1000);

Console.WriteLine("\n=== Done ===");

await host.StopAsync();
