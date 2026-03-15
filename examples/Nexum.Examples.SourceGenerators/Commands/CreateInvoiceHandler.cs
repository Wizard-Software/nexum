using Nexum.Abstractions;
using Nexum.Examples.SourceGenerators.Domain;

namespace Nexum.Examples.SourceGenerators.Commands;

// SG Tier 1: This handler is discovered via [CommandHandler] and registered by
//            NexumHandlerRegistry.AddNexumHandlers() as an explicit ServiceDescriptor —
//            no reflection at runtime, fully NativeAOT-safe.
[CommandHandler]
public sealed class CreateInvoiceHandler : ICommandHandler<CreateInvoiceCommand, Guid>
{
    private static readonly List<Invoice> s_invoices = [];

    public ValueTask<Guid> HandleAsync(CreateInvoiceCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        s_invoices.Add(new Invoice(id, command.Customer, command.Amount));
        Console.WriteLine($"  [Handler] Created invoice {id:N} for {command.Customer}: {command.Amount:C}");
        return ValueTask.FromResult(id);
    }

    internal static Invoice? Find(Guid id) =>
        s_invoices.FirstOrDefault(i => i.Id == id);
}
