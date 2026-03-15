using Nexum.Abstractions;
using Nexum.Examples.WebApi.Domain;

namespace Nexum.Examples.WebApi.Queries;

// GET /api/orders/{id} — looks up a single order by its Guid.
// Returns null when the order does not exist (produces 200 with null body).
public sealed record GetOrderQuery(Guid Id) : IQuery<Order?>;
