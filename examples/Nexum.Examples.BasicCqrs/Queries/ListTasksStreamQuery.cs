using Nexum.Abstractions;
using Nexum.Examples.BasicCqrs.Domain;

namespace Nexum.Examples.BasicCqrs.Queries;

public sealed record ListTasksStreamQuery : IStreamQuery<TaskItem>;
