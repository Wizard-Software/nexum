using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Results.FluentValidation;
using Nexum.Examples.ResultsValidation.Commands;
using Nexum.Examples.ResultsValidation.ResultDemo;

Console.WriteLine("=== Nexum ResultsValidation Example ===\n");

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register all command handlers discovered in this assembly
        services.AddNexum(assemblies: typeof(Program).Assembly);

        // Register FluentValidation behavior + scan validators from this assembly.
        // Validators run as pipeline behaviors before handlers — validation failures
        // are returned as Result.Fail(ValidationNexumError) instead of exceptions.
        services.AddNexumFluentValidation(assemblies: typeof(Program).Assembly);
    })
    .Build();

using var scope = host.Services.CreateScope();
var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

// -----------------------------------------------------------------------
// Demo 1: Dispatch a valid command — expect Result.Ok with a new Guid
// -----------------------------------------------------------------------
Console.WriteLine("--- Demo 1: Valid command ---");
var validCommand = new CreateProductCommand("Ergonomic Keyboard", 129.99m);
var validResult = await commands.DispatchAsync(validCommand);

if (validResult.IsSuccess)
{
    Console.WriteLine($"  Success: product id = {validResult.Value}");
}

// -----------------------------------------------------------------------
// Demo 2: Dispatch an invalid command — empty name and negative price.
//         FluentValidation behavior intercepts before the handler runs
//         and returns Result.Fail(ValidationNexumError).
// -----------------------------------------------------------------------
Console.WriteLine("\n--- Demo 2: Invalid command (validation failure) ---");
var invalidCommand = new CreateProductCommand(Name: string.Empty, Price: -10m);
var invalidResult = await commands.DispatchAsync(invalidCommand);

if (invalidResult.IsFailure)
{
    var error = invalidResult.Error;
    Console.WriteLine($"  Failure: code={error.Code}, message={error.Message}");

    // Pattern match to inspect individual validation failures
    if (error is ValidationNexumError validationError)
    {
        Console.WriteLine($"  Validation failures ({validationError.Failures.Count}):");
        foreach (var failure in validationError.Failures)
        {
            Console.WriteLine($"    - [{failure.PropertyName}] {failure.ErrorMessage}");
        }
    }
}

// -----------------------------------------------------------------------
// Demo 3: Result chaining — Map / Bind / GetValueOrDefault
// -----------------------------------------------------------------------
ResultChainingDemo.Run();

Console.WriteLine("\n=== Done ===");
