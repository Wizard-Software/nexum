using Nexum.Abstractions;

namespace Nexum.Examples.NexumEndpoints.Commands;

// [NexumEndpoint] — SG generates endpoint from this attribute: HTTP method + route pattern.
// Source Generator emits a MapPost("/api/tickets") endpoint that deserializes the request body
// to CreateTicketCommand, dispatches it, and returns 200 OK with the new Guid.
[NexumEndpoint(NexumHttpMethod.Post, "/api/tickets")]
public sealed record CreateTicketCommand(string Title) : ICommand<Guid>;
