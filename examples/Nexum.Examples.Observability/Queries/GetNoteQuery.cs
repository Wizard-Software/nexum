using Nexum.Abstractions;
using Nexum.Examples.Observability.Domain;

namespace Nexum.Examples.Observability.Queries;

// Dispatching this query produces an Activity span named "Nexum.Query GetNoteQuery"
// visible in the console exporter output.
public sealed record GetNoteQuery(Guid Id) : IQuery<Note?>;
