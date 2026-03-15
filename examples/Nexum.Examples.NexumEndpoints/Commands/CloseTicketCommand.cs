using Nexum.Abstractions;

namespace Nexum.Examples.NexumEndpoints.Commands;

// [NexumEndpoint] — SG generates endpoint from this attribute: HTTP method + route pattern.
// IVoidCommand → SG generates endpoint returning 204 NoContent.
// The {id} segment is bound from the route via [AsParameters] on the generated endpoint.
[NexumEndpoint(NexumHttpMethod.Put, "/api/tickets/{id}/close")]
public sealed record CloseTicketCommand(Guid Id) : IVoidCommand;
