namespace Nexum.Examples.Advanced.Polymorphic;

public sealed record CreditCardPaymentCommand(decimal Amount)
    : BasePaymentCommand("CreditCard", Amount);
