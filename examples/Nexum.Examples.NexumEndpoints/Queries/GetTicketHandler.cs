using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;

namespace Nexum.Examples.NexumEndpoints.Queries;

// SG Tier 1: [QueryHandler] marks this handler for NexumHandlerRegistry.AddNexumHandlers()
//            — registered as explicit ServiceDescriptor with no runtime reflection.
[QueryHandler]
public sealed class GetTicketHandler(ConcurrentDictionary<Guid, Ticket> store)
    : IQueryHandler<GetTicketQuery, Ticket>
{
    public ValueTask<Ticket> HandleAsync(GetTicketQuery query, CancellationToken ct = default)
    {
        if (!store.TryGetValue(query.Id, out Ticket? ticket))
        {
            throw new KeyNotFoundException($"Ticket '{query.Id}' not found.");
        }

        return ValueTask.FromResult(ticket);
    }
}
