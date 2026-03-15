using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Commands;

public sealed record PlaceOrderCommand(string ProductName, int Quantity) : ICommand<string>;
