using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;

namespace Nexum.Examples.NexumEndpoints.Commands;

[CommandHandler]
public sealed class CloseTicketHandler(ConcurrentDictionary<Guid, Ticket> store)
    : ICommandHandler<CloseTicketCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(CloseTicketCommand command, CancellationToken ct = default)
    {
        if (store.TryGetValue(command.Id, out Ticket? ticket))
        {
            store[command.Id] = ticket with { Status = "Closed" };
        }

        return ValueTask.FromResult(Unit.Value);
    }
}
