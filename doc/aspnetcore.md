# ASP.NET Core Integration

The `Nexum.Extensions.AspNetCore` package provides middleware, endpoint mapping, and Problem Details integration for seamless use of Nexum in ASP.NET Core applications.

## Installation

```bash
dotnet add package Nexum.Extensions.AspNetCore
```

## Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNexum();
builder.Services.AddNexumAspNetCore();

var app = builder.Build();

app.UseNexum(); // Adds Nexum middleware (Problem Details error handling)

// Map endpoints...

app.Run();
```

## Endpoint Mapping

Map commands and queries directly to HTTP endpoints without writing boilerplate:

### Commands (POST)

```csharp
// Command with return value -> POST, returns 200 OK with result
app.MapNexumCommand<CreateOrderCommand, Guid>("/api/orders");

// Void command -> POST, returns 204 No Content
app.MapNexumCommand<DeleteOrderCommand>("/api/orders/{orderId}");
```

Equivalent to:

```csharp
app.MapPost("/api/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var result = await dispatcher.DispatchAsync(command, ct);
    return Results.Ok(result);
});
```

### Queries (GET)

```csharp
// Query -> GET, returns 200 OK with result
app.MapNexumQuery<GetOrderQuery, OrderDto?>("/api/orders/{orderId}");
```

Query parameters are bound from the URL using `[AsParameters]`. Define your query record with properties matching route/query string parameters:

```csharp
public record GetOrderQuery(Guid OrderId) : IQuery<OrderDto?>;

public record SearchOrdersQuery(
    string? CustomerId,
    int Page = 1,
    int PageSize = 20) : IQuery<IReadOnlyList<OrderDto>>;
```

### OpenAPI Metadata

Mapped endpoints automatically include OpenAPI metadata:

- Commands: `Produces<TResult>(200)` + `ProducesProblem(400)`
- Void commands: `Produces(204)` + `ProducesProblem(400)`
- Queries: `Produces<TResult>(200)` + `ProducesProblem(400)`

### Chaining

All `MapNexum*` methods return `RouteHandlerBuilder`, so you can chain standard ASP.NET Core endpoint configuration:

```csharp
app.MapNexumCommand<CreateOrderCommand, Guid>("/api/orders")
    .RequireAuthorization("admin")
    .WithTags("Orders")
    .WithName("CreateOrder");

app.MapNexumQuery<GetOrderQuery, OrderDto?>("/api/orders/{orderId}")
    .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(5)))
    .WithTags("Orders");
```

## Problem Details

The middleware converts Nexum exceptions into RFC 7807 Problem Details responses:

### Built-in Mappings

| Exception | Status Code | Title |
|-----------|------------|-------|
| `NexumHandlerNotFoundException` | 404 | Handler Not Found |
| `NexumDispatchDepthExceededException` | 500 | Dispatch Depth Exceeded |

### Custom Exception Mappings

```csharp
builder.Services.AddNexumAspNetCore(configureProblemDetails: options =>
{
    options.IncludeExceptionDetails = builder.Environment.IsDevelopment();

    options.ExceptionMappings[typeof(ValidationException)] = ex =>
        new ProblemDetails
        {
            Status = 422,
            Title = "Validation Failed",
            Detail = ex.Message
        };

    options.ExceptionMappings[typeof(UnauthorizedAccessException)] = ex =>
        new ProblemDetails
        {
            Status = 403,
            Title = "Forbidden",
            Detail = "You do not have permission to perform this action."
        };
});
```

### NexumProblemDetailsOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ExceptionMappings` | `Dictionary<Type, Func<Exception, ProblemDetails?>>` | Pre-configured | Maps exception types to Problem Details |
| `IncludeExceptionDetails` | `bool` | `false` | Include stack traces in responses (development only) |

## Endpoint Options

Configure default status codes and error mapping:

```csharp
builder.Services.AddNexumAspNetCore(configureEndpoints: options =>
{
    options.SuccessStatusCode = 200;
    options.FailureStatusCode = 400;
    options.ErrorToProblemDetails = error => new ProblemDetails
    {
        Status = 400,
        Title = "Request Failed",
        Detail = error?.ToString()
    };
});
```

### NexumEndpointOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SuccessStatusCode` | `int` | `200` | HTTP status for successful results |
| `FailureStatusCode` | `int` | `400` | HTTP status for failed results |
| `ErrorToProblemDetails` | `Func<object, ProblemDetails?>?` | `null` | Custom error-to-ProblemDetails mapping |

## Result Pattern Integration

When using `Nexum.Results` with ASP.NET Core, register a result adapter so that the endpoint mapper can distinguish success from failure:

```csharp
builder.Services.AddNexumResultAdapter<NexumResultAdapter<OrderDto>>();
```

With an adapter registered, `MapNexumCommand` and `MapNexumQuery` automatically:
- Return the success value with `SuccessStatusCode` when `IsSuccess` is `true`
- Return a Problem Details response with `FailureStatusCode` when `IsFailure` is `true`

## Full Example

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNexum();
builder.Services.AddNexumTelemetry();
builder.Services.AddNexumAspNetCore(
    configureProblemDetails: pd =>
    {
        pd.IncludeExceptionDetails = builder.Environment.IsDevelopment();
    });

var app = builder.Build();

app.UseNexum();

var orders = app.MapGroup("/api/orders").WithTags("Orders");

orders.MapNexumCommand<CreateOrderCommand, Guid>("/")
    .RequireAuthorization();

orders.MapNexumCommand<DeleteOrderCommand>("/{orderId}")
    .RequireAuthorization("admin");

orders.MapNexumQuery<GetOrderQuery, OrderDto?>("/{orderId}");

orders.MapNexumQuery<SearchOrdersQuery, IReadOnlyList<OrderDto>>("/");

app.Run();
```
