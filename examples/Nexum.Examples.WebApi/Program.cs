using System.Collections.Concurrent;
using Nexum.Examples.WebApi.Domain;
using Nexum.Examples.WebApi.Commands;
using Nexum.Examples.WebApi.Queries;
using Nexum.Extensions.AspNetCore;
using Nexum.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Step 1: Register all Nexum handlers discovered in this assembly.
// AddNexum scans the assembly for ICommandHandler<,>, IQueryHandler<,>, etc.
builder.Services.AddNexum(assemblies: typeof(Program).Assembly);

// Step 2: Register Nexum ASP.NET Core integration.
// This registers the NexumResultEndpointFilter and configures ProblemDetails mapping
// so that any NexumException thrown in a handler is converted to an RFC 9457 response.
builder.Services.AddNexumAspNetCore();

// Step 3: Register the built-in .NET 10 OpenAPI support.
// Accessible at /openapi/v1.json — no Swashbuckle required.
builder.Services.AddOpenApi();

// Step 4: Register the shared in-memory order store as a singleton.
// Both CreateOrderHandler and GetOrderHandler depend on this dictionary.
builder.Services.AddSingleton<ConcurrentDictionary<Guid, Order>>();

var app = builder.Build();

// Step 5: Add the Nexum exception-handling middleware.
// Must be placed before endpoint mappings so it wraps all handler invocations.
app.UseNexum();

// Step 6: Map the OpenAPI descriptor endpoint (serves the JSON schema).
app.MapOpenApi();

// Step 7: Map the POST /api/orders command endpoint.
// MapNexumCommand deserializes the request body to CreateOrderCommand,
// dispatches it through the Nexum pipeline, and returns 200 OK with the new Guid.
app.MapNexumCommand<CreateOrderCommand, Guid>("/api/orders");

// Step 8: Map the GET /api/orders/{id} query endpoint.
// MapNexumQuery binds the {id} route segment to GetOrderQuery.Id via [AsParameters],
// dispatches through the pipeline, and returns 200 OK with the Order (or null).
app.MapNexumQuery<GetOrderQuery, Order?>("/api/orders/{id}");

app.Run();
