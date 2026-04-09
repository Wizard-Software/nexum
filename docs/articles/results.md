# Result Pattern

The `Nexum.Results` package provides an explicit error-handling primitive: `Result<T, TError>`. Results are opt-in — you can mix them freely with exception-based handlers in the same application.

## The type

```csharp
public readonly record struct Result<T, TError>
{
    public bool IsSuccess { get; }
    public T Value { get; }      // valid only if IsSuccess
    public TError Error { get; } // valid only if !IsSuccess

    public static Result<T, TError> Success(T value);
    public static Result<T, TError> Failure(TError error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TError, TOut> onFailure);
}
```

A convenience alias defaults the error type to `NexumError`:

```csharp
public readonly record struct Result<T>
{
    // same shape, TError = NexumError
}
```

`NexumError` is a lightweight error type with a `Code`, `Message`, and optional `Metadata` dictionary. Use it when you don't need a project-specific error hierarchy.

## Using results in handlers

```csharp
public record CreateOrderCommand(string CustomerId, List<string> Items) : ICommand<Result<Guid>>;

[CommandHandler]
public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IOrderRepository _repo;
    public CreateOrderHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken ct)
    {
        if (command.Items.Count == 0)
            return Result<Guid>.Failure(new NexumError("order.empty", "At least one item is required"));

        var order = Order.Create(command.CustomerId, command.Items);
        await _repo.SaveAsync(order, ct);
        return Result<Guid>.Success(order.Id);
    }
}
```

## Matching results

```csharp
var result = await dispatcher.DispatchAsync(new CreateOrderCommand(...), ct);

return result.Match(
    onSuccess: id => Results.Created($"/orders/{id}", new { Id = id }),
    onFailure: err => Results.Problem(err.Message, statusCode: 400));
```

## FluentValidation integration

The `Nexum.Results.FluentValidation` package adds a pipeline behavior that runs FluentValidation validators and converts failures into `Result<T>.Failure(...)` automatically — without throwing.

```csharp
services.AddNexum();
services.AddNexumResultsFluentValidation();
services.AddValidatorsFromAssemblyContaining<CreateOrderCommandValidator>();
```

```csharp
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Items).NotEmpty();
    }
}
```

When the command returns a `Result<T>`, the behavior short-circuits to `Result<T>.Failure` on validation failure. For commands that do not return a `Result<T>`, the behavior falls back to throwing a `NexumValidationException`.

## Composition over inheritance

Nexum's result type is a `readonly record struct` — zero-allocation, equality by value, no inheritance hierarchy. This is intentional: results compose (`Bind`, `Map`, `Match`) rather than extending a base class.
