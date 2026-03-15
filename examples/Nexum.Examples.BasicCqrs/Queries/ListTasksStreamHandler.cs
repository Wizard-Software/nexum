using System.Runtime.CompilerServices;
using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.Queries;

public sealed class ListTasksStreamHandler : IStreamQueryHandler<ListTasksStreamQuery, TaskItem>
{
    public async IAsyncEnumerable<TaskItem> HandleAsync(
        ListTasksStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var item in TaskItem.GetAll())
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
        }
    }
}
