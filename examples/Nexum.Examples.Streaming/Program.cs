using Nexum.Examples.Streaming.Domain;
using Nexum.Examples.Streaming.Hubs;
using Nexum.Examples.Streaming.StreamQueries;
using Nexum.Extensions.DependencyInjection;
using Nexum.Streaming;

// =============================================================================
// Nexum Streaming Example — SignalR + Server-Sent Events (SSE)
//
// Demonstrates:
//   1. IStreamQuery<TResult> — async stream query dispatched via IQueryDispatcher
//   2. IStreamNotification<TItem> — pub/sub notification with merged async streams
//   3. NexumStreamHubBase — SignalR hub with typed streaming methods
//   4. MapNexumStream<TQuery, TResult> — SSE endpoint using .NET 10 TypedResults.ServerSentEvents()
//
// Run:
//   dotnet run --project examples/Nexum.Examples.Streaming
//   Open http://localhost:5000 in a browser to see live SSE price updates
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// 1. Register Nexum handlers via assembly scanning
builder.Services.AddNexum(assemblies: typeof(Program).Assembly);

// 2. Register Nexum Streaming services (IStreamNotificationPublisher, StreamMerger)
builder.Services.AddNexumStreaming();

// 3. Register SignalR services for NexumStreamHubBase integration
builder.Services.AddNexumSignalR();

var app = builder.Build();

// 4. Serve static files (wwwroot/index.html — SSE demo client)
app.UseStaticFiles();

// 5. Map SSE endpoint: GET /api/prices/stream
//    Uses .NET 10 TypedResults.ServerSentEvents() under the hood (ADR-011 D6)
//    Query parameters bound via [AsParameters], e.g. ?Symbol=AAPL
app.MapNexumStream<GetPriceUpdatesQuery, PriceUpdate>("/api/prices/stream");

// 6. Map SignalR hub: /hubs/prices
//    Clients connect via SignalR JS client and call hub.stream("StreamPrices", ...)
app.MapHub<PriceStreamHub>("/hubs/prices");

// 7. Default route redirects to the SSE demo page
app.MapGet("/", () => Results.Redirect("/index.html"));

app.Run();
