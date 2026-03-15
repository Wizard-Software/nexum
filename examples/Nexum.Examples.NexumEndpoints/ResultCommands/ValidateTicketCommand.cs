using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;
using Nexum.Results;

namespace Nexum.Examples.NexumEndpoints.ResultCommands;

// [NexumEndpoint] — SG generates endpoint from this attribute: HTTP method + route pattern.
// Returns Result<Ticket>: SG-generated endpoint maps Result<T> to HTTP inline (no reflection):
//   IsSuccess  → 200 OK with Ticket body
//   !IsSuccess → ProblemDetails (RFC 9457) via WithNexumResultMapping()
[NexumEndpoint(NexumHttpMethod.Post, "/api/tickets/validate")]
public sealed record ValidateTicketCommand(string Title) : ICommand<Result<Ticket>>;
