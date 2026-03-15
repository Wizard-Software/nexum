using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Commands;

public sealed record CreateInvoiceCommand(string Customer, decimal Amount) : ICommand<Guid>;
