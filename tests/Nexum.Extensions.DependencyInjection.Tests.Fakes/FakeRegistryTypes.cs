using Nexum.Abstractions;

namespace Nexum.Extensions.DependencyInjection.Tests.Fakes;

public sealed record FakeRegistryCommand(string Value) : ICommand<string>;

public sealed class FakeRegistryHandler : ICommandHandler<FakeRegistryCommand, string>
{
    public ValueTask<string> HandleAsync(FakeRegistryCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(command.Value);
}
