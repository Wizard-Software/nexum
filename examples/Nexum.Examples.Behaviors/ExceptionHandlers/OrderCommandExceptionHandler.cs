using Nexum.Abstractions;
using Nexum.Examples.Behaviors.Commands;

namespace Nexum.Examples.Behaviors.ExceptionHandlers;

public sealed class OrderCommandExceptionHandler : ICommandExceptionHandler<PlaceOrderCommand, InvalidOperationException>
{
    public ValueTask HandleAsync(PlaceOrderCommand command, InvalidOperationException exception, CancellationToken ct = default)
    {
        Console.WriteLine($"  [EXCEPTION HANDLER] Command failed: {exception.Message}");
        Console.WriteLine($"  [EXCEPTION HANDLER] Command was: PlaceOrder for {command.ProductName} x{command.Quantity}");
        return ValueTask.CompletedTask;
    }
}
