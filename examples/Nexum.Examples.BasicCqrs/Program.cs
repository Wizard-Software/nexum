using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Examples.BasicCqrs.Commands;
using Nexum.Examples.BasicCqrs.VoidCommands;
using Nexum.Examples.BasicCqrs.Queries;
using Nexum.Examples.BasicCqrs.Notifications;

Console.WriteLine("=== Nexum BasicCqrs Example ===\n");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddNexum(assemblies: typeof(Program).Assembly);
    })
    .Build();

using var scope = host.Services.CreateScope();
var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
var queries = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
var notifications = scope.ServiceProvider.GetRequiredService<INotificationPublisher>();

// 1. Create tasks
Console.WriteLine("--- Creating tasks ---");
var id1 = await commands.DispatchAsync(new CreateTaskCommand("Buy groceries"));
_ = await commands.DispatchAsync(new CreateTaskCommand("Write documentation"));
_ = await commands.DispatchAsync(new CreateTaskCommand("Review PR"));

// 2. Publish notifications
Console.WriteLine("\n--- Publishing notifications ---");
await notifications.PublishAsync(new TaskCreatedNotification(id1, "Buy groceries"));

// 3. Query single task
Console.WriteLine("\n--- Querying single task ---");
var task = await queries.DispatchAsync(new GetTaskQuery(id1));
Console.WriteLine($"  Found: #{task?.Id} {task?.Title} (Done: {task?.IsDone})");

// 4. Stream all tasks
Console.WriteLine("\n--- Streaming all tasks ---");
await foreach (var t in queries.StreamAsync(new ListTasksStreamQuery()))
{
    Console.WriteLine($"  #{t.Id}: {t.Title} (Done: {t.IsDone})");
}

// 5. Void command — mark as done
Console.WriteLine("\n--- Marking task as done ---");
await commands.DispatchAsync(new MarkTaskDoneCommand(id1));

// 6. Verify change
Console.WriteLine("\n--- Verifying change ---");
var updated = await queries.DispatchAsync(new GetTaskQuery(id1));
Console.WriteLine($"  Task #{updated?.Id}: Done = {updated?.IsDone}");

Console.WriteLine("\n=== Done ===");
