# Pipeline Behaviors

Nexum supports pipeline behaviors using the Russian doll model. Each behavior wraps the next handler in the chain, enabling cross-cutting concerns like validation, logging, caching, and transactions.

Unlike MediatR's single `IPipelineBehavior<,>`, Nexum provides **separate** behavior interfaces for commands, queries, and stream queries. A command validation behavior will never accidentally run in the query pipeline.

## Behavior Interfaces

### Command Behaviors

```csharp
public interface ICommandBehavior<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    ValueTask<TResult> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResult> next,
        CancellationToken ct = default);
}
```

### Query Behaviors

```csharp
public interface IQueryBehavior<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    ValueTask<TResult> HandleAsync(
        TQuery query,
        QueryHandlerDelegate<TResult> next,
        CancellationToken ct = default);
}
```

### Stream Query Behaviors

```csharp
public interface IStreamQueryBehavior<in TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    IAsyncEnumerable<TResult> HandleAsync(
        TQuery query,
        StreamQueryHandlerDelegate<TResult> next,
        CancellationToken ct = default);
}
```

## Delegate Types

The `next` parameter represents the next step in the pipeline (either the next behavior or the final handler):

```csharp
delegate ValueTask<TResult> CommandHandlerDelegate<TResult>(CancellationToken ct);
delegate ValueTask<TResult> QueryHandlerDelegate<TResult>(CancellationToken ct);
delegate IAsyncEnumerable<TResult> StreamQueryHandlerDelegate<TResult>(CancellationToken ct);
```

Note: Unlike MediatR where `next()` takes no arguments, Nexum delegates accept `CancellationToken` -- always forward it via `next(ct)`.

## Examples

### Logging Behavior

```csharp
[BehaviorOrder(1)]
public class LoggingCommandBehavior<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly ILogger<LoggingCommandBehavior<TCommand, TResult>> _logger;

    public LoggingCommandBehavior(ILogger<LoggingCommandBehavior<TCommand, TResult>> logger)
        => _logger = logger;

    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
    {
        _logger.LogInformation("Dispatching {CommandType}", typeof(TCommand).Name);
        var stopwatch = Stopwatch.StartNew();

        var result = await next(ct).ConfigureAwait(false);

        _logger.LogInformation("Dispatched {CommandType} in {Elapsed}ms",
            typeof(TCommand).Name, stopwatch.ElapsedMilliseconds);
        return result;
    }
}
```

### Validation Behavior

```csharp
[BehaviorOrder(2)]
public class ValidationCommandBehavior<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly IEnumerable<IValidator<TCommand>> _validators;

    public ValidationCommandBehavior(IEnumerable<IValidator<TCommand>> validators)
        => _validators = validators;

    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
    {
        var failures = _validators
            .Select(v => v.Validate(command))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct).ConfigureAwait(false);
    }
}
```

### Transaction Behavior

```csharp
[BehaviorOrder(10)]
public class TransactionCommandBehavior<TCommand, TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly IDbContext _db;

    public TransactionCommandBehavior(IDbContext db) => _db = db;

    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
    {
        await using var transaction = await _db.BeginTransactionAsync(ct);

        var result = await next(ct).ConfigureAwait(false);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return result;
    }
}
```

### Caching Query Behavior

```csharp
[BehaviorOrder(1)]
public class CachingQueryBehavior<TQuery, TResult>
    : IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    private readonly IDistributedCache _cache;

    public CachingQueryBehavior(IDistributedCache cache) => _cache = cache;

    public async ValueTask<TResult> HandleAsync(
        TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct)
    {
        var cacheKey = $"query:{typeof(TQuery).Name}:{query.GetHashCode()}";
        var cached = await _cache.GetStringAsync(cacheKey, ct);

        if (cached is not null)
            return JsonSerializer.Deserialize<TResult>(cached)!;

        var result = await next(ct).ConfigureAwait(false);

        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) },
            ct);

        return result;
    }
}
```

## Execution Order

Use `[BehaviorOrder(int)]` to control the order behaviors execute. Lower values run first (outermost in the Russian doll).

```
Request
  └── [BehaviorOrder(1)] Logging
        └── [BehaviorOrder(2)] Validation
              └── [BehaviorOrder(10)] Transaction
                    └── Handler
```

If two behaviors have the same order, execution order is unspecified. Always assign explicit, distinct order values.

You can also override behavior order at registration time:

```csharp
builder.Services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>), order: 1);
builder.Services.AddNexumBehavior(typeof(ValidationCommandBehavior<,>), order: 2);
```

## Registration

Register behaviors via DI extension methods:

```csharp
// Open generic behaviors (apply to all command/query types)
builder.Services.AddNexumBehavior(typeof(LoggingCommandBehavior<,>), order: 1);
builder.Services.AddNexumBehavior(typeof(ValidationCommandBehavior<,>), order: 2);

// Closed generic behaviors (apply to specific types only)
builder.Services.AddNexumBehavior(
    typeof(CachingQueryBehavior<GetOrderQuery, OrderDto?>));
```

## Behavior Lifetime

Behaviors are registered as **Transient** by default. Override via the registration method:

```csharp
builder.Services.AddNexumBehavior(
    typeof(CachingQueryBehavior<,>),
    lifetime: NexumLifetime.Scoped);
```

## ConfigureAwait(false)

As a library convention, all internal `await` calls in Nexum use `.ConfigureAwait(false)`. Follow the same pattern in your behaviors to avoid deadlocks in non-ASP.NET Core hosts:

```csharp
var result = await next(ct).ConfigureAwait(false);
```
