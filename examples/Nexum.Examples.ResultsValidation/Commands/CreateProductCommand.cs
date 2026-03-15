using Nexum.Abstractions;
using Nexum.Results;

namespace Nexum.Examples.ResultsValidation.Commands;

// Command returns Result<Guid> — validation failures are returned as Result.Fail,
// not exceptions. The FluentValidation behavior intercepts this before the handler runs.
public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<Result<Guid>>;
