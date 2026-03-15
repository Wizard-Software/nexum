using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.VoidCommands;

public sealed class MarkTaskDoneHandler : ICommandHandler<MarkTaskDoneCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(MarkTaskDoneCommand command, CancellationToken ct = default)
    {
        TaskItem.MarkDone(command.TaskId);
        Console.WriteLine($"  Task #{command.TaskId} marked as done");
        return ValueTask.FromResult(Unit.Value);
    }
}
