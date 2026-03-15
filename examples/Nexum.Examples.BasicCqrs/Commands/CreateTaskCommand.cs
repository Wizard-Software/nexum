using Nexum.Abstractions;

namespace Nexum.Examples.BasicCqrs.Commands;

public sealed record CreateTaskCommand(string Title) : ICommand<int>;
