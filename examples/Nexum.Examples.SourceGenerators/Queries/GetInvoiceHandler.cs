using Nexum.Abstractions;
using Nexum.Examples.SourceGenerators.Commands;
using Nexum.Examples.SourceGenerators.Domain;

namespace Nexum.Examples.SourceGenerators.Queries;

// SG Tier 1: This handler is discovered via [QueryHandler] and registered by
//            NexumHandlerRegistry.AddNexumHandlers() as an explicit ServiceDescriptor.
[QueryHandler]
public sealed class GetInvoiceHandler : IQueryHandler<GetInvoiceQuery, Invoice>
{
    public ValueTask<Invoice> HandleAsync(GetInvoiceQuery query, CancellationToken ct = default)
    {
        var invoice = CreateInvoiceHandler.Find(query.Id)
            ?? throw new InvalidOperationException($"Invoice {query.Id:N} not found");
        Console.WriteLine($"  [Handler] Found invoice {query.Id:N}: {invoice.Customer} {invoice.Amount:C}");
        return ValueTask.FromResult(invoice);
    }
}
