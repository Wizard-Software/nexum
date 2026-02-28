# API Reference

Complete public API surface of the Nexum library, organized by package.

## Nexum.Abstractions

Zero-dependency package containing all contracts.

### Core Interfaces

#### ICommand

```csharp
// Non-generic marker interface for all commands
public interface ICommand;

// Generic command with typed result
public interface ICommand<out TResult> : ICommand;

// Void command convenience alias
public interface IVoidCommand : ICommand<Unit>;
```

#### IQuery

```csharp
// Non-generic marker interface for all queries
public interface IQuery;

// Generic query with typed result
public interface IQuery<out TResult> : IQuery;
```

#### IStreamQuery

```csharp
// Streaming query returning IAsyncEnumerable<TResult>
public interface IStreamQuery<out TResult>;
```

#### INotification

```csharp
// Marker interface for domain events
public interface INotification;
```

### Dispatchers

#### ICommandDispatcher

```csharp
public interface ICommandDispatcher
{
    ValueTask<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken ct = default);
}
```

#### IQueryDispatcher

```csharp
public interface IQueryDispatcher
{
    ValueTask<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query,
        CancellationToken ct = default);

    IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IStreamQuery<TResult> query,
        CancellationToken ct = default);
}
```

#### INotificationPublisher

```csharp
public interface INotificationPublisher
{
    ValueTask PublishAsync<TNotification>(
        TNotification notification,
        PublishStrategy? strategy = null,
        CancellationToken ct = default)
        where TNotification : INotification;
}
```

### Handlers

#### ICommandHandler

```csharp
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    ValueTask<TResult> HandleAsync(TCommand command, CancellationToken ct = default);
}
```

#### IQueryHandler

```csharp
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    ValueTask<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

#### IStreamQueryHandler

```csharp
public interface IStreamQueryHandler<in TQuery, TResult>
    where TQuery : IStreamQuery<TResult>
{
    IAsyncEnumerable<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

#### INotificationHandler

```csharp
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    ValueTask HandleAsync(TNotification notification, CancellationToken ct = default);
}
```

### Behaviors

#### ICommandBehavior

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

#### IQueryBehavior

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

#### IStreamQueryBehavior

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

#### Delegates

```csharp
public delegate ValueTask<TResult> CommandHandlerDelegate<TResult>(CancellationToken ct);
public delegate ValueTask<TResult> QueryHandlerDelegate<TResult>(CancellationToken ct);
public delegate IAsyncEnumerable<TResult> StreamQueryHandlerDelegate<TResult>(CancellationToken ct);
```

### Exception Handlers

#### ICommandExceptionHandler

```csharp
public interface ICommandExceptionHandler<in TCommand, in TException>
    where TCommand : ICommand
    where TException : Exception
{
    ValueTask HandleAsync(TCommand command, TException exception, CancellationToken ct = default);
}
```

#### IQueryExceptionHandler

```csharp
public interface IQueryExceptionHandler<in TQuery, in TException>
    where TQuery : IQuery
    where TException : Exception
{
    ValueTask HandleAsync(TQuery query, TException exception, CancellationToken ct = default);
}
```

#### INotificationExceptionHandler

```csharp
public interface INotificationExceptionHandler<in TNotification, in TException>
    where TNotification : INotification
    where TException : Exception
{
    ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default);
}
```

### Attributes

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandHandlerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class QueryHandlerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class StreamQueryHandlerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class NotificationHandlerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public sealed class BehaviorOrderAttribute(int order) : Attribute
{
    public int Order { get; } = order;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class HandlerLifetimeAttribute(NexumLifetime lifetime) : Attribute
{
    public NexumLifetime Lifetime { get; } = lifetime;
}
```

### Enums

#### PublishStrategy

```csharp
public enum PublishStrategy
{
    Sequential,
    Parallel,
    StopOnException,
    FireAndForget
}
```

#### NexumLifetime

```csharp
public enum NexumLifetime
{
    Transient,
    Scoped,
    Singleton
}
```

### Utility Types

#### Unit

```csharp
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    public static readonly Unit Value;
}
```

### Extension Points

#### IResultAdapter

```csharp
public interface IResultAdapter<in TResult>
{
    bool IsSuccess(TResult result);
    object? GetValue(TResult result);
    object? GetError(TResult result);
}
```

### Exceptions

#### NexumHandlerNotFoundException

```csharp
public class NexumHandlerNotFoundException : InvalidOperationException
{
    public Type RequestType { get; }
    public NexumHandlerNotFoundException(Type requestType, string handlerInterfaceName);
}
```

#### NexumDispatchDepthExceededException

```csharp
public class NexumDispatchDepthExceededException : InvalidOperationException
{
    public int MaxDepth { get; }
    public NexumDispatchDepthExceededException(int maxDepth);
}
```

---

## Nexum

Runtime dispatchers and pipeline engine.

### NexumOptions

```csharp
public sealed class NexumOptions
{
    public PublishStrategy DefaultPublishStrategy { get; set; } = PublishStrategy.Sequential;
    public TimeSpan FireAndForgetTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public int FireAndForgetChannelCapacity { get; set; } = 1000;
    public TimeSpan FireAndForgetDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxDispatchDepth { get; set; } = 16;
}
```

### Dispatchers (sealed, internal construction)

- `CommandDispatcher : ICommandDispatcher` -- Thread-safe, singleton-compatible
- `QueryDispatcher : IQueryDispatcher` -- Thread-safe, singleton-compatible
- `NotificationPublisher : INotificationPublisher` -- Thread-safe, singleton-compatible

---

## Nexum.Extensions.DependencyInjection

### NexumServiceCollectionExtensions

```csharp
public static class NexumServiceCollectionExtensions
{
    public static IServiceCollection AddNexum(
        this IServiceCollection services,
        Action<NexumOptions>? configure = null,
        params Assembly[] assemblies);

    public static IServiceCollection AddNexumHandler<TService, TImplementation>(
        this IServiceCollection services,
        NexumLifetime lifetime = NexumLifetime.Scoped);

    public static IServiceCollection AddNexumBehavior(
        this IServiceCollection services,
        Type behaviorType,
        int? order = null,
        NexumLifetime lifetime = NexumLifetime.Transient);

    public static IServiceCollection AddNexumExceptionHandler<THandler>(
        this IServiceCollection services,
        NexumLifetime lifetime = NexumLifetime.Transient);

    public static IServiceCollection AddNexumResultAdapter<TAdapter>(
        this IServiceCollection services);
}
```

---

## Nexum.OpenTelemetry

### NexumTelemetryOptions

```csharp
public sealed class NexumTelemetryOptions
{
    public bool EnableTracing { get; set; } = true;
    public bool EnableMetrics { get; set; } = true;
    public string ActivitySourceName { get; set; } = "Nexum.Cqrs";
}
```

### NexumInstrumentation

```csharp
public sealed class NexumInstrumentation : IDisposable
{
    public ActivitySource ActivitySource { get; }
    public Meter Meter { get; }
    public Counter<long> DispatchCount { get; }
    public Histogram<double> DispatchDuration { get; }
    public Counter<long> NotificationCount { get; }
    public static string GetTypeName(Type type);
    public void RecordDispatchMetrics(string typeName, string status, long startTimestamp);
    public void RecordNotificationMetrics(string typeName, string strategy);
}
```

### Extension Method

```csharp
public static IServiceCollection AddNexumTelemetry(
    this IServiceCollection services,
    Action<NexumTelemetryOptions>? configure = null);
```

---

## Nexum.Results

### Result<TValue, TError>

```csharp
public readonly struct Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public TValue Value { get; }   // Throws if IsFailure
    public TError Error { get; }   // Throws if IsSuccess

    public static Result<TValue, TError> Ok(TValue value);
    public static Result<TValue, TError> Fail(TError error);

    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper);
    public Result<TNew, TError> Bind<TNew>(Func<TValue, Result<TNew, TError>> binder);
    public TValue GetValueOrDefault(TValue fallback);
}
```

### Result<TValue>

```csharp
// Convenience alias: Result<TValue> = Result<TValue, NexumError>
public readonly struct Result<TValue>
{
    // Same API as Result<TValue, TError> with TError = NexumError
}
```

### NexumError

```csharp
public record NexumError(string Code, string Message, Exception? InnerException = null);
```

### Built-in Adapters

```csharp
public sealed class NexumResultAdapter<TValue, TError> : IResultAdapter<Result<TValue, TError>>;
public sealed class NexumResultAdapter<TValue> : IResultAdapter<Result<TValue>>;
```

---

## Nexum.Extensions.AspNetCore

### Service Registration

```csharp
public static IServiceCollection AddNexumAspNetCore(
    this IServiceCollection services,
    Action<NexumProblemDetailsOptions>? configureProblemDetails = null,
    Action<NexumEndpointOptions>? configureEndpoints = null);
```

### Middleware

```csharp
public static IApplicationBuilder UseNexum(this IApplicationBuilder app);
```

### Endpoint Mapping

```csharp
public static class NexumEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder MapNexumCommand<TCommand, TResult>(
        this IEndpointRouteBuilder endpoints, string pattern)
        where TCommand : ICommand<TResult>;

    public static RouteHandlerBuilder MapNexumCommand<TCommand>(
        this IEndpointRouteBuilder endpoints, string pattern)
        where TCommand : IVoidCommand;

    public static RouteHandlerBuilder MapNexumQuery<TQuery, TResult>(
        this IEndpointRouteBuilder endpoints, string pattern)
        where TQuery : IQuery<TResult>;
}
```

### Options

```csharp
public sealed class NexumProblemDetailsOptions
{
    public Dictionary<Type, Func<Exception, ProblemDetails?>> ExceptionMappings { get; }
    public bool IncludeExceptionDetails { get; set; } = false;
}

public sealed class NexumEndpointOptions
{
    public int SuccessStatusCode { get; set; } = 200;
    public int FailureStatusCode { get; set; } = 400;
    public Func<object, ProblemDetails?>? ErrorToProblemDetails { get; set; }
}
```

---

## Nexum.Batching

### IBatchQueryHandler

```csharp
public interface IBatchQueryHandler<in TQuery, TKey, TResult>
    where TQuery : IQuery<TResult>
    where TKey : notnull
{
    TKey GetKey(TQuery query);
    ValueTask<IReadOnlyDictionary<TKey, TResult>> HandleAsync(
        IReadOnlyList<TQuery> queries, CancellationToken ct = default);
}
```

### NexumBatchingOptions

```csharp
public sealed class NexumBatchingOptions
{
    public TimeSpan BatchWindow { get; set; } = TimeSpan.FromMilliseconds(10);
    public int MaxBatchSize { get; set; } = 100;
    public TimeSpan DrainTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
```
