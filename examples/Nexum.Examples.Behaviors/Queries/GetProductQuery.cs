using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Queries;

public sealed record GetProductQuery(string ProductName) : IQuery<decimal>;
