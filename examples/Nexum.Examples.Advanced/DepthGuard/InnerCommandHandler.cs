using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.DepthGuard;

public sealed class InnerCommandHandler(ICommandDispatcher dispatcher) : ICommandHandler<InnerCommand, string>
{
    public async ValueTask<string> HandleAsync(InnerCommand command, CancellationToken ct = default)
    {
        if (command.ExceedDepth)
        {
            // Dispatch recursively to trigger depth guard
            var deeper = await dispatcher.DispatchAsync(new OuterCommand(ExceedDepth: true), ct);
            return $"Inner -> {deeper}";
        }

        return "Inner";
    }
}
