#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Nexum;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

try
{
    // Setup DI container manually (AOT-safe registration)
    var services = new ServiceCollection();

    // Register Nexum core services
    services.AddSingleton(new NexumOptions { MaxDispatchDepth = int.MaxValue });
    services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
    services.TryAddSingleton<ExceptionHandlerResolver>();
    services.TryAddSingleton<ICommandDispatcher, CommandDispatcher>();

    // Use Source Generator registration
    Nexum.NativeAot.SmokeTest.NexumHandlerRegistry.AddNexumHandlers(services);

    // Build service provider
    var serviceProvider = services.BuildServiceProvider();

    // Resolve dispatcher and execute test command
    var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
    var result = await dispatcher.DispatchAsync(new SmokeCommand("test")).ConfigureAwait(false);

    // Verify result
    if (result != "NativeAOT OK")
    {
        Console.Error.WriteLine($"Expected 'NativeAOT OK' but got '{result}'");
        return 1;
    }

    Console.WriteLine("NativeAOT smoke test passed!");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Smoke test failed: {ex}");
    return 1;
}

// Test command
internal sealed record SmokeCommand(string Input) : ICommand<string>;

// Test handler
[CommandHandler]
internal sealed class SmokeCommandHandler : ICommandHandler<SmokeCommand, string>
{
    public ValueTask<string> HandleAsync(SmokeCommand command, CancellationToken ct = default)
        => ValueTask.FromResult("NativeAOT OK");
}
