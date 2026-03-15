using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.DepthGuard;

public sealed class OuterCommandHandler(ICommandDispatcher dispatcher) : ICommandHandler<OuterCommand, string>
{
    public async ValueTask<string> HandleAsync(OuterCommand command, CancellationToken ct = default)
    {
        var inner = await dispatcher.DispatchAsync(new InnerCommand(command.ExceedDepth), ct);
        return $"Outer -> {inner}";
    }
}
