using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.Observability.Domain;

namespace Nexum.Examples.Observability.Queries;

// Looks up a note by Id from the shared in-memory store.
// Returns null when the note is not found (no exception thrown — nullable result pattern).
public sealed class GetNoteHandler(ConcurrentDictionary<Guid, Note> store)
    : IQueryHandler<GetNoteQuery, Note?>
{
    public ValueTask<Note?> HandleAsync(GetNoteQuery query, CancellationToken ct = default)
    {
        store.TryGetValue(query.Id, out Note? note);
        return ValueTask.FromResult(note);
    }
}
