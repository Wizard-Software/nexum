using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

[HandlerLifetime(NexumLifetime.Singleton)]
public sealed class SingletonHandler : ICommandHandler<SingletonCommand, string>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<string> HandleAsync(SingletonCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId.ToString());
}
