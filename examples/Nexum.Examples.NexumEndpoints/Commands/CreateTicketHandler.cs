using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;

namespace Nexum.Examples.NexumEndpoints.Commands;

// SG Tier 1: [CommandHandler] marks this handler for NexumHandlerRegistry.AddNexumHandlers()
//            — registered as explicit ServiceDescriptor with no runtime reflection.
[CommandHandler]
public sealed class CreateTicketHandler(ConcurrentDictionary<Guid, Ticket> store)
    : ICommandHandler<CreateTicketCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateTicketCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var ticket = new Ticket(id, command.Title, "Open");
        store[id] = ticket;
        return ValueTask.FromResult(id);
    }
}
