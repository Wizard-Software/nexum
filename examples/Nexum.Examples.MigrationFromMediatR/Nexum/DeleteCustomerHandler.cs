using Nexum.Abstractions;
using Nexum.Examples.MigrationFromMediatR.Shared;

namespace Nexum.Examples.MigrationFromMediatR.Nexum;

// AFTER (native Nexum handler): Returns ValueTask<Unit> (not Task<T>).
// No MediatR dependency — this is a pure Nexum handler.
// Existing MediatR handlers and this handler coexist in the same DI container.
public sealed class DeleteCustomerHandler(CustomerStore store)
    : ICommandHandler<DeleteCustomerCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(DeleteCustomerCommand command, CancellationToken ct = default)
    {
        var removed = store.Remove(command.Id);
        Console.WriteLine(removed
            ? $"  [Nexum Handler] Deleted customer: {command.Id}"
            : $"  [Nexum Handler] Customer not found for deletion: {command.Id}");
        return new ValueTask<Unit>(Unit.Value);
    }
}
