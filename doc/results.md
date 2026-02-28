# Result Pattern

The `Nexum.Results` package provides a native `Result<T, TError>` type for explicit error handling without exceptions.

## Installation

```bash
dotnet add package Nexum.Results
```

## Result Types

### `Result<TValue, TError>`

A discriminated union that is either a success with a value or a failure with an error:

```csharp
// Create results
var success = Result<Order, OrderError>.Ok(order);
var failure = Result<Order, OrderError>.Fail(new OrderError("NOT_FOUND", "Order not found"));

// Check state
if (result.IsSuccess)
    Console.WriteLine($"Order: {result.Value}");

if (result.IsFailure)
    Console.WriteLine($"Error: {result.Error}");
```

### `Result<TValue>`

A convenience alias that uses `NexumError` as the default error type:

```csharp
// Equivalent to Result<Order, NexumError>
var success = Result<Order>.Ok(order);
var failure = Result<Order>.Fail(new NexumError("NOT_FOUND", "Order not found"));
```

### `NexumError`

The built-in error type:

```csharp
public record NexumError(string Code, string Message, Exception? InnerException = null);
```

`NexumError` is not sealed -- you can inherit from it for domain-specific errors:

```csharp
public record ValidationError(string Code, string Message, IDictionary<string, string[]> Errors)
    : NexumError(Code, Message);

public record NotFoundError(string Code, string Message)
    : NexumError(Code, Message);
```

## Using Results in Handlers

```csharp
public record CreateOrderCommand(string CustomerId, List<OrderItemDto> Items)
    : ICommand<Result<Guid>>;

[CommandHandler]
public class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _repo;
    private readonly ICustomerRepository _customers;

    public CreateOrderHandler(IOrderRepository repo, ICustomerRepository customers)
    {
        _repo = repo;
        _customers = customers;
    }

    public async ValueTask<Result<Guid>> HandleAsync(
        CreateOrderCommand command, CancellationToken ct)
    {
        var customer = await _customers.GetByIdAsync(command.CustomerId, ct);
        if (customer is null)
            return Result<Guid>.Fail(new NexumError("CUSTOMER_NOT_FOUND",
                $"Customer {command.CustomerId} not found"));

        if (command.Items.Count == 0)
            return Result<Guid>.Fail(new NexumError("EMPTY_ORDER",
                "Order must contain at least one item"));

        var order = Order.Create(customer, command.Items);
        await _repo.SaveAsync(order, ct);

        return Result<Guid>.Ok(order.Id);
    }
}
```

## Consuming Results

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);

    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value}", new { Id = result.Value })
        : Results.BadRequest(new { result.Error.Code, result.Error.Message });
});
```

## Functional Operations

### Map

Transform the success value:

```csharp
Result<OrderDto> dto = result.Map(order => order.ToDto());
```

### Bind

Chain operations that return results:

```csharp
Result<ShippingLabel> label = result.Bind(order => shippingService.CreateLabel(order));
```

### GetValueOrDefault

Extract the value with a fallback:

```csharp
Order order = result.GetValueOrDefault(Order.Empty);
```

## IResultAdapter

The `IResultAdapter<TResult>` interface enables Nexum to introspect result types for OpenTelemetry and ASP.NET Core integration:

```csharp
public interface IResultAdapter<in TResult>
{
    bool IsSuccess(TResult result);
    object? GetValue(TResult result);
    object? GetError(TResult result);
}
```

Built-in adapters are provided for `Result<TValue, TError>` and `Result<TValue>`. Register them:

```csharp
builder.Services.AddNexumResultAdapter<NexumResultAdapter<OrderDto>>();
```

When a result adapter is registered, Nexum automatically:
- Tags OpenTelemetry spans with success/failure status based on the result
- Maps failures to Problem Details in ASP.NET Core endpoints

## Custom Error Types

Use `Result<TValue, TError>` with your own error types:

```csharp
public abstract record OrderError(string Code, string Message);
public record OrderNotFound(Guid OrderId) : OrderError("ORDER_NOT_FOUND", $"Order {OrderId} not found");
public record InsufficientStock(string Sku) : OrderError("INSUFFICIENT_STOCK", $"Not enough stock for {Sku}");

public record PlaceOrderCommand(Guid CustomerId, List<OrderItem> Items)
    : ICommand<Result<Order, OrderError>>;
```
