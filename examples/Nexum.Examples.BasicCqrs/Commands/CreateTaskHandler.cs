using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.Commands;

public sealed class CreateTaskHandler : ICommandHandler<CreateTaskCommand, int>
{
    public ValueTask<int> HandleAsync(CreateTaskCommand command, CancellationToken ct = default)
    {
        var task = TaskItem.Add(command.Title);
        Console.WriteLine($"  Created task #{task.Id}: {task.Title}");
        return ValueTask.FromResult(task.Id);
    }
}
