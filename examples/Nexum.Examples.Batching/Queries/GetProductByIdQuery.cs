using Nexum.Abstractions;
using Nexum.Examples.Batching.Domain;

namespace Nexum.Examples.Batching.Queries;

/// <summary>
/// Query to retrieve a single product by its ID.
/// When dispatched concurrently, the batching layer collects
/// multiple instances and delivers them as one batch to the handler.
/// </summary>
public record GetProductByIdQuery(int ProductId) : IQuery<Product>;
