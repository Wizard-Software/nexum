using System.Collections.Concurrent;

namespace Nexum.Examples.BasicCqrs.Domain;

public sealed record TaskItem(int Id, string Title, bool IsDone)
{
    private static readonly ConcurrentDictionary<int, TaskItem> s_store = new();
    private static int s_nextId;

    public static TaskItem Add(string title)
    {
        var id = Interlocked.Increment(ref s_nextId);
        var item = new TaskItem(id, title, IsDone: false);
        s_store[id] = item;
        return item;
    }

    public static TaskItem? Get(int id) =>
        s_store.TryGetValue(id, out var item) ? item : null;

    public static IEnumerable<TaskItem> GetAll() => s_store.Values;

    public static void MarkDone(int id)
    {
        if (s_store.TryGetValue(id, out var item))
        {
            s_store[id] = item with { IsDone = true };
        }
    }
}
