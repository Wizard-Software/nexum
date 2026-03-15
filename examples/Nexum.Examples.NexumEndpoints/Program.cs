using System.Collections.Concurrent;
using Nexum.Examples.NexumEndpoints;          // SG-generated NexumEndpointRegistration (MapNexumEndpoints)
using Nexum.Examples.NexumEndpoints.Domain;
using Nexum.Extensions.AspNetCore;
using Nexum.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Step 1: SG-generated DI registration (Tier 1).
// Nexum.SourceGenerators scans this assembly for [CommandHandler], [QueryHandler], etc.
// and emits NexumHandlerRegistry.AddNexumHandlers() — zero reflection, NativeAOT-safe.
builder.Services.AddNexum(assemblies: typeof(Program).Assembly);

// Step 2: Register Nexum ASP.NET Core integration.
// Registers NexumResultEndpointFilter for runtime Result<T>→HTTP mapping and
// configures ProblemDetails options for NexumException → RFC 9457 responses.
builder.Services.AddNexumAspNetCore();

// Step 3: Register the built-in .NET 10 OpenAPI support.
// Accessible at /openapi/v1.json — no Swashbuckle required.
builder.Services.AddOpenApi();

// Step 4: Register the shared in-memory ticket store as a singleton.
// CreateTicketHandler, CloseTicketHandler, and GetTicketHandler share this store.
builder.Services.AddSingleton<ConcurrentDictionary<Guid, Ticket>>();

var app = builder.Build();

// Step 5: Add the Nexum exception-handling middleware.
// Must be placed before endpoint mappings so it wraps all handler invocations.
app.UseNexum();

// Step 6: Map the OpenAPI descriptor endpoint.
// Visit /openapi/v1.json to see the generated schema.
app.MapOpenApi();

// Step 7: SG-generated endpoint registration — one call instead of manual MapNexumCommand/Query.
// Nexum.SourceGenerators scans [NexumEndpoint] attributes and emits MapNexumEndpoints() in
// NexumEndpointRegistration class — zero reflection, NativeAOT-safe.
//
// Endpoints registered by [NexumEndpoint]:
//   POST   /api/tickets               ← CreateTicketCommand   (returns 200 OK with Guid)
//   PUT    /api/tickets/{id}/close    ← CloseTicketCommand    (IVoidCommand → 204 NoContent)
//   GET    /api/tickets/{id}          ← GetTicketQuery        (returns 200 OK with Ticket?)
//   POST   /api/tickets/validate      ← ValidateTicketCommand (Result<Ticket> → 200 OK / 400 Problem)
app.MapNexumEndpoints();

app.Run();
