using Nexum.Abstractions;

namespace Nexum.Results.FluentValidation.Tests.Fixtures;

public record CreateOrderCommand(string Name, decimal Amount) : ICommand<Result<Guid>>;

public record SimpleCommand(string Value) : ICommand<Guid>;

public record TwoParamResultCommand(string Value) : ICommand<Result<Guid, NexumError>>;
