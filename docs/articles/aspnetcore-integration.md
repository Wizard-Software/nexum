# ASP.NET Core Integration

The `Nexum.Extensions.AspNetCore` package provides tooling to dispatch Nexum commands and queries from ASP.NET Core minimal APIs and MVC controllers, including Problem Details integration for error responses.

## Minimal API dispatch

Minimal APIs and Nexum fit together without any helpers — model-bind straight into the command:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNexum();

var app = builder.Build();

app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandDispatcher dispatcher,
    CancellationToken ct) =>
{
    var id = await dispatcher.DispatchAsync(command, ct);
    return Results.Created($"/orders/{id}", new { Id = id });
});

app.MapGet("/orders/{id:guid}", async (
    Guid id,
    IQueryDispatcher dispatcher,
    CancellationToken ct) =>
{
    var order = await dispatcher.DispatchAsync(new GetOrderQuery(id), ct);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

app.Run();
```

## Stream endpoints

`IStreamQuery<T>` pairs naturally with `TypedResults.Stream` or server-sent events:

```csharp
app.MapGet("/orders/{id:guid}/events", (
    Guid id,
    IQueryDispatcher dispatcher,
    CancellationToken ct) =>
{
    var stream = dispatcher.StreamAsync(new TailOrderEventsQuery(id), ct);
    return TypedResults.Stream(stream);
});
```

## Problem Details

The package adds a Problem Details middleware that translates common Nexum exceptions into RFC 7807 responses:

```csharp
app.UseNexumProblemDetails();
```

Built-in mappings:

| Exception | HTTP status | `type` |
|-----------|-------------|--------|
| `NexumValidationException` | 400 | `https://tools.ietf.org/html/rfc9110#section-15.5.1` |
| `HandlerNotFoundException` | 501 | `urn:nexum:handler-not-found` |
| `DispatchDepthExceededException` | 500 | `urn:nexum:dispatch-depth-exceeded` |

You can add custom mappings by registering `IProblemDetailsMapper` implementations in DI.

## Endpoint conventions

For projects that want a light-weight endpoint-per-command pattern, the package provides `MapCommand` and `MapQuery` helpers:

```csharp
app.MapCommand<CreateOrderCommand, Guid>("/orders", HttpVerb.Post);
app.MapQuery<GetOrderQuery, OrderDto?>("/orders/{OrderId:guid}");
```

These helpers bind the route, model-bind the command, dispatch, and return `Results.Ok` or `Results.NotFound` as appropriate.
