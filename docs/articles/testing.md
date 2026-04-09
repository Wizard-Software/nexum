# Testing

`Nexum.Testing` provides helpers that make it easy to test handlers, behaviors, and entire dispatch pipelines in isolation. The package has no dependencies on any specific test framework — use it with xUnit, NUnit, TUnit, or MSTest.

## `NexumTestHost`

A lightweight composition root that wires Nexum into a minimal `IServiceCollection`, applies your fakes, and gives you dispatchers to drive tests:

```csharp
[Fact]
public async Task CreateOrder_persists_and_returns_id()
{
    var repo = new FakeOrderRepository();
    var host = NexumTestHost.Create(services =>
    {
        services.AddSingleton<IOrderRepository>(repo);
        services.AddNexum(assemblies: typeof(CreateOrderHandler).Assembly);
    });

    var id = await host.Commands.DispatchAsync(
        new CreateOrderCommand("C-1", ["item-1"]),
        TestContext.Current.CancellationToken);

    Assert.NotEqual(Guid.Empty, id);
    Assert.Single(repo.Saved);
}
```

`host.Commands`, `host.Queries`, and `host.Publisher` expose the three dispatchers directly.

## Fake dispatchers

For unit tests that don't actually want to run the pipeline, Nexum ships fake dispatchers that record calls:

```csharp
var commands = new FakeCommandDispatcher();
commands.Setup<CreateOrderCommand, Guid>(cmd => Guid.Parse("aaaa..."));

var controller = new OrdersController(commands);
var response = await controller.Create(new CreateOrderRequest(...));

Assert.Single(commands.Dispatched<CreateOrderCommand>());
```

Available fakes:

- `FakeCommandDispatcher`
- `FakeQueryDispatcher`
- `FakeNotificationPublisher`

Each one lets you register responses per type and later assert on recorded calls (`Dispatched<T>()`, `Published<T>()`, etc.).

## Testing behaviors in isolation

A behavior is just a class — instantiate it directly and call `HandleAsync` with a test `next` delegate:

```csharp
[Fact]
public async Task ValidationBehavior_throws_when_invalid()
{
    var validators = new[] { new TestValidator(valid: false) };
    var behavior = new ValidationBehavior<CreateOrderCommand, Guid>(validators);

    await Assert.ThrowsAsync<ValidationException>(() =>
        behavior.HandleAsync(
            new CreateOrderCommand("", []),
            next: () => ValueTask.FromResult(Guid.Empty),
            ct: TestContext.Current.CancellationToken).AsTask());
}
```

## Integration tests with the real pipeline

When you want to exercise the entire pipeline — behaviors, validators, handlers — use `NexumTestHost.CreateFromAssembly(typeof(CreateOrderHandler).Assembly)`. It scans the assembly, registers every handler and every behavior, and gives you a ready-to-use host.
