using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;

namespace Nexum.Examples.NexumEndpoints.Queries;

// [NexumEndpoint] — SG generates endpoint from this attribute: HTTP method + route pattern.
// GET query: the {id} route segment is bound to the Id property via [AsParameters] on the
// generated endpoint — zero manual route binding code required.
// Returns non-nullable Ticket; handler throws KeyNotFoundException if ticket is not found.
[NexumEndpoint(NexumHttpMethod.Get, "/api/tickets/{id}")]
public sealed record GetTicketQuery(Guid Id) : IQuery<Ticket>;
