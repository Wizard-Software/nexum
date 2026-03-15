using Nexum.Abstractions;

namespace Nexum.Examples.BasicCqrs.VoidCommands;

public sealed record MarkTaskDoneCommand(int TaskId) : IVoidCommand;
