# Pipeline Behaviors

Pipeline behaviors wrap handler execution with cross-cutting concerns — validation, logging, transactions, caching, authorization. Nexum uses the **Russian doll** model: each behavior calls `next()` to invoke the inner layer and returns after the inner layer completes.

Unlike MediatR, Nexum provides **separate** behavior interfaces for commands, queries, and stream queries. This gives you compile-time type safety — a command validation behavior will never accidentally run in the query pipeline.

## The three behavior interfaces

```csharp
public interface ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct);
}

public interface IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    ValueTask<TResult> HandleAsync(
        TQuery query,
        QueryHandlerDelegate<TResult> next,
        CancellationToken ct);
}

public interface IStreamQueryBehavior<TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    IAsyncEnumerable<TResult> HandleAsync(
        TQuery query,
        StreamQueryHandlerDelegate<TResult> next,
        CancellationToken ct);
}
```

## Example: validation behavior for commands

```csharp
public sealed class ValidationBehavior<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TCommand>> validators) => _validators = validators;

    public async ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct)
    {
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(command, ct).ConfigureAwait(false);
            if (!result.IsValid)
                throw new ValidationException(result.Errors);
        }

        return await next().ConfigureAwait(false);
    }
}
```

## Ordering

Behavior order is controlled by the `[BehaviorOrder(int)]` attribute. Lower values run first (outermost). Behaviors without the attribute use `int.MaxValue` and run last.

```csharp
[BehaviorOrder(1)]
public sealed class LoggingBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult> { ... }

[BehaviorOrder(2)]
public sealed class ValidationBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult> { ... }

[BehaviorOrder(3)]
public sealed class TransactionBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult> { ... }
```

Execution order: `Logging → Validation → Transaction → Handler → Transaction → Validation → Logging`.

## Registering behaviors

```csharp
services.AddNexum();
services.AddScoped(typeof(ICommandBehavior<,>), typeof(LoggingBehavior<,>));
services.AddScoped(typeof(ICommandBehavior<,>), typeof(ValidationBehavior<,>));
services.AddScoped(typeof(ICommandBehavior<,>), typeof(TransactionBehavior<,>));
```

Open generics let a single behavior apply to every command. You can also register closed-generic behaviors for specific command types.

## Separation from exception handlers

Exception handling belongs outside the pipeline — use `ICommandExceptionHandler<TCommand, TException>` instead of swallowing exceptions in a behavior. Exception handlers are side-effect only (logging, metrics, alerts); the dispatcher always re-throws after invoking them. See [Commands and Queries](commands-and-queries.md) for details.
