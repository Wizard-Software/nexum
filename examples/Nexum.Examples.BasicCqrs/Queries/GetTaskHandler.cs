using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.Queries;

public sealed class GetTaskHandler : IQueryHandler<GetTaskQuery, TaskItem?>
{
    public ValueTask<TaskItem?> HandleAsync(GetTaskQuery query, CancellationToken ct = default) =>
        ValueTask.FromResult(TaskItem.Get(query.TaskId));
}
