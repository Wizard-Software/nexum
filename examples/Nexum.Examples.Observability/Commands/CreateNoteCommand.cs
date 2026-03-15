using Nexum.Abstractions;

namespace Nexum.Examples.Observability.Commands;

// Dispatching this command produces an Activity span named "Nexum.Command CreateNoteCommand"
// visible in the console exporter output.
public sealed record CreateNoteCommand(string Title, string Content) : ICommand<Guid>;
