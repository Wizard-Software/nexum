namespace Nexum.Examples.Advanced.Polymorphic;

public sealed record BankTransferPaymentCommand(decimal Amount)
    : BasePaymentCommand("BankTransfer", Amount);
