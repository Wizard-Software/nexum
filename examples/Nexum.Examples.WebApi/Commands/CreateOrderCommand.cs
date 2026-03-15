using Nexum.Abstractions;

namespace Nexum.Examples.WebApi.Commands;

// POST /api/orders body — creates a new order and returns the new order's Guid.
public sealed record CreateOrderCommand(string Product, int Quantity, decimal UnitPrice)
    : ICommand<Guid>;
