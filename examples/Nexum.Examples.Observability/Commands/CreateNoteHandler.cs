using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Examples.Observability.Domain;

namespace Nexum.Examples.Observability.Commands;

// Stores the new note in the shared in-memory dictionary and returns the generated Guid.
// The ConcurrentDictionary<Guid, Note> singleton is shared with GetNoteHandler.
public sealed class CreateNoteHandler(ConcurrentDictionary<Guid, Note> store)
    : ICommandHandler<CreateNoteCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateNoteCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        var note = new Note(id, command.Title, command.Content);
        store[id] = note;
        return ValueTask.FromResult(id);
    }
}
