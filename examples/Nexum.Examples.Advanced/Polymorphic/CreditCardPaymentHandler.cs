using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Polymorphic;

public sealed class CreditCardPaymentHandler : ICommandHandler<CreditCardPaymentCommand, string>
{
    public ValueTask<string> HandleAsync(CreditCardPaymentCommand command, CancellationToken ct = default)
        => ValueTask.FromResult($"processed via CreditCardPaymentHandler (amount: {command.Amount:C})");
}
