using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

[HandlerLifetime(NexumLifetime.Transient)]
public sealed class TransientHandler : ICommandHandler<TransientCommand, string>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<string> HandleAsync(TransientCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId.ToString());
}
