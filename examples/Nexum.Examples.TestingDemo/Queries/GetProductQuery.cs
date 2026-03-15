using Nexum.Abstractions;
using Nexum.Examples.TestingDemo.Domain;

namespace Nexum.Examples.TestingDemo.Queries;

public sealed record GetProductQuery(Guid Id) : IQuery<Product?>;
