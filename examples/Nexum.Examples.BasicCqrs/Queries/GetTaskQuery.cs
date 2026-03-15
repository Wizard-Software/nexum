using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.Queries;

public sealed record GetTaskQuery(int TaskId) : IQuery<TaskItem?>;
