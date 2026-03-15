using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Examples.Advanced.DepthGuard;
using Nexum.Examples.Advanced.Lifetime;
using Nexum.Examples.Advanced.Polymorphic;
using Nexum.Examples.Advanced.Configuration;

Console.WriteLine("=== Nexum Advanced Example ===\n");

// -----------------------------------------------------------------------
// Section 1: MaxDispatchDepth — re-entrancy protection
// Configure MaxDispatchDepth = 3: Outer(1) -> Inner(2) succeeds,
// but Inner -> Outer -> Inner(3) -> Outer(4) exceeds the limit.
// -----------------------------------------------------------------------
Console.WriteLine("--- Section 1: MaxDispatchDepth (depth guard) ---");

var depthHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddNexum(
            options => options.MaxDispatchDepth = 3,
            assemblies: typeof(Program).Assembly);
    })
    .Build();

using (var scope = depthHost.Services.CreateScope())
{
    var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

    // Normal dispatch: depth 1 (Outer) -> depth 2 (Inner) — succeeds
    var result = await commands.DispatchAsync(new OuterCommand());
    Console.WriteLine($"  Normal dispatch succeeded: \"{result}\"");

    // Recursive dispatch: triggers depth guard at depth 4
    try
    {
        await commands.DispatchAsync(new OuterCommand(ExceedDepth: true));
        Console.WriteLine("  ERROR: Expected NexumDispatchDepthExceededException was not thrown.");
    }
    catch (NexumDispatchDepthExceededException ex)
    {
        Console.WriteLine($"  Depth guard triggered: NexumDispatchDepthExceededException (MaxDepth={ex.MaxDepth})");
    }
}

Console.WriteLine();

// -----------------------------------------------------------------------
// Section 2: [HandlerLifetime] — different handler lifetimes
// Each handler stores a Guid set at construction time.
// Transient: new instance per dispatch → different InstanceIds
// Scoped:    one instance per scope   → same InstanceId within scope
// Singleton: one instance for all     → same InstanceId across scopes
// -----------------------------------------------------------------------
Console.WriteLine("--- Section 2: [HandlerLifetime] — handler lifetimes ---");

var lifetimeHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddNexum(assemblies: typeof(Program).Assembly);
    })
    .Build();

using (var scope = lifetimeHost.Services.CreateScope())
{
    var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

    var transient1 = await commands.DispatchAsync(new TransientCommand());
    var transient2 = await commands.DispatchAsync(new TransientCommand());
    Console.WriteLine($"  Transient dispatch 1: {transient1}");
    Console.WriteLine($"  Transient dispatch 2: {transient2}");
    Console.WriteLine($"  Transient same instance: {transient1 == transient2} (expected: False)");

    var scoped1 = await commands.DispatchAsync(new ScopedCommand());
    var scoped2 = await commands.DispatchAsync(new ScopedCommand());
    Console.WriteLine($"  Scoped dispatch 1: {scoped1}");
    Console.WriteLine($"  Scoped dispatch 2: {scoped2}");
    Console.WriteLine($"  Scoped same instance: {scoped1 == scoped2} (expected: True)");

    var singleton1 = await commands.DispatchAsync(new SingletonCommand());
    var singleton2 = await commands.DispatchAsync(new SingletonCommand());
    Console.WriteLine($"  Singleton dispatch 1: {singleton1}");
    Console.WriteLine($"  Singleton dispatch 2: {singleton2}");
    Console.WriteLine($"  Singleton same instance: {singleton1 == singleton2} (expected: True)");
}

Console.WriteLine();

// -----------------------------------------------------------------------
// Section 3: Polymorphic handler resolution
// CreditCardPaymentCommand and BankTransferPaymentCommand both extend
// BasePaymentCommand : ICommand<string>. Nexum resolves the concrete
// handler for each derived type via its polymorphic resolver cache.
// -----------------------------------------------------------------------
Console.WriteLine("--- Section 3: Polymorphic handler resolution ---");

var polymorphicHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddNexum(assemblies: typeof(Program).Assembly);
    })
    .Build();

using (var scope = polymorphicHost.Services.CreateScope())
{
    var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

    var creditCard = await commands.DispatchAsync(new CreditCardPaymentCommand(149.99m));
    Console.WriteLine($"  CreditCard:   {creditCard}");

    var bankTransfer = await commands.DispatchAsync(new BankTransferPaymentCommand(2500.00m));
    Console.WriteLine($"  BankTransfer: {bankTransfer}");
}

Console.WriteLine();

// -----------------------------------------------------------------------
// Section 4: NexumOptions configuration
// NexumOptions is registered as a singleton and can be injected directly.
// -----------------------------------------------------------------------
Console.WriteLine("--- Section 4: NexumOptions configuration ---");

var optionsHost = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddNexum(
            options =>
            {
                options.DefaultPublishStrategy = PublishStrategy.Parallel;
                options.FireAndForgetTimeout = TimeSpan.FromSeconds(60);
                options.MaxDispatchDepth = 8;
            },
            assemblies: typeof(Program).Assembly);
    })
    .Build();

using (var scope = optionsHost.Services.CreateScope())
{
    var commands = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
    var config = await commands.DispatchAsync(new ConfigDemoCommand());
    Console.WriteLine($"  NexumOptions: {config}");
}

Console.WriteLine("\n=== Done ===");
