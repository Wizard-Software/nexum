using System.Collections.Concurrent;
using Nexum.Examples.Observability.Domain;
using Nexum.Examples.Observability.Commands;
using Nexum.Examples.Observability.Queries;
using Nexum.Extensions.DependencyInjection;
using Nexum.Extensions.AspNetCore;
using Nexum.OpenTelemetry;
using OpenTelemetry.Trace;

// =============================================================================
// Nexum.Examples.Observability
// Demonstrates Nexum OpenTelemetry integration with console trace export.
//
// When you POST /api/notes, the console will show Activity spans like:
//   Activity.DisplayName: Nexum.Command CreateNoteCommand
//   Activity.DisplayName: Nexum.Query GetNoteQuery
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

// 1. Register Nexum handlers by scanning this assembly.
//    Must be called BEFORE AddNexumTelemetry().
builder.Services.AddNexum(assemblies: typeof(Program).Assembly);

// 2. Wrap Nexum dispatchers with OpenTelemetry tracing and metrics decorators.
//    EnableTracing = true  → ICommandDispatcher / IQueryDispatcher create Activity spans.
//    EnableMetrics = true  → dispatchers record request counters and duration histograms.
//    Must be called AFTER AddNexum() so the decorators can wrap the registered dispatchers.
builder.Services.AddNexumTelemetry(opts =>
{
    opts.EnableTracing = true;
    opts.EnableMetrics = true;
    // opts.ActivitySourceName defaults to "Nexum.Cqrs" — used below in AddSource()
});

// 3. Configure the OpenTelemetry SDK to listen to the Nexum activity source
//    and export traces to the console.
//    Console output will include one Activity per dispatched command/query.
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Nexum.Cqrs")      // matches NexumTelemetryOptions.ActivitySourceName default
        .AddConsoleExporter());        // prints Activity details to stdout

// 4. Register Nexum ASP.NET Core services (ProblemDetails + endpoint filter support).
//    Also register ProblemDetails so the Nexum middleware can serialize error responses.
builder.Services.AddProblemDetails();
builder.Services.AddNexumAspNetCore();

// 5. Shared in-memory store — singleton so both handlers access the same dictionary.
builder.Services.AddSingleton(new ConcurrentDictionary<Guid, Note>());

var app = builder.Build();

// 6. Add Nexum middleware: catches Nexum exceptions and returns ProblemDetails responses.
app.UseNexum();

// POST /api/notes — dispatches CreateNoteCommand, returns the new note's Guid.
// Console will show: Activity.DisplayName = "Nexum.Command CreateNoteCommand"
app.MapNexumCommand<CreateNoteCommand, Guid>("/api/notes");

// GET /api/notes/{id} — dispatches GetNoteQuery, returns the Note or null.
// Console will show: Activity.DisplayName = "Nexum.Query GetNoteQuery"
app.MapNexumQuery<GetNoteQuery, Note?>("/api/notes/{id}");

app.Run();
