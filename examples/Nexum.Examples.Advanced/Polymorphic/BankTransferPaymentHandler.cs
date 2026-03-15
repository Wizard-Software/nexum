using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Polymorphic;

public sealed class BankTransferPaymentHandler : ICommandHandler<BankTransferPaymentCommand, string>
{
    public ValueTask<string> HandleAsync(BankTransferPaymentCommand command, CancellationToken ct = default)
        => ValueTask.FromResult($"processed via BankTransferPaymentHandler (amount: {command.Amount:C})");
}
