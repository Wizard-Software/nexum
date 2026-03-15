using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Commands;

public sealed class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, string>
{
    public ValueTask<string> HandleAsync(PlaceOrderCommand command, CancellationToken ct = default)
    {
        if (command.Quantity <= 0)
        {
            throw new InvalidOperationException("Quantity must be positive");
        }

        var orderId = $"ORD-{Guid.NewGuid():N}"[..12];
        Console.WriteLine($"  [Handler] Placing order for {command.ProductName} x{command.Quantity} → {orderId}");
        return ValueTask.FromResult(orderId);
    }
}
