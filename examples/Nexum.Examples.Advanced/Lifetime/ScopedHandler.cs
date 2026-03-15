using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

// Default handler lifetime is Scoped — no attribute needed
public sealed class ScopedHandler : ICommandHandler<ScopedCommand, string>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<string> HandleAsync(ScopedCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId.ToString());
}
