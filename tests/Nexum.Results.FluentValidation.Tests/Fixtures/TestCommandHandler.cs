using Nexum.Abstractions;

namespace Nexum.Results.FluentValidation.Tests.Fixtures;

[CommandHandler]
public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Result<Guid>>
{
    public ValueTask<Result<Guid>> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(Result<Guid>.Ok(Guid.NewGuid()));
}

[CommandHandler]
public class SimpleCommandHandler : ICommandHandler<SimpleCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(SimpleCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(Guid.NewGuid());
}

[CommandHandler]
public class TwoParamResultCommandHandler : ICommandHandler<TwoParamResultCommand, Result<Guid, NexumError>>
{
    public ValueTask<Result<Guid, NexumError>> HandleAsync(TwoParamResultCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(Result<Guid, NexumError>.Ok(Guid.NewGuid()));
}
