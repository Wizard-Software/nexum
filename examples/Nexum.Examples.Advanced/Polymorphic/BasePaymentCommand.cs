using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Polymorphic;

public abstract record BasePaymentCommand(string Type, decimal Amount) : ICommand<string>;
