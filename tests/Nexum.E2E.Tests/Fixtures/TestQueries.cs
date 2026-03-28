using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

// Basic query
public sealed record GetItemQuery(Guid Id) : IQuery<ItemDto?>;

// Stream query
public sealed record ListItemsStreamQuery(int Count = 10) : IStreamQuery<ItemDto>;

// Query for caching behavior
public sealed record GetProductPriceQuery(string ProductName) : IQuery<decimal>;

// Stream query for filtering behavior
public sealed record ListPricesStreamQuery(decimal MinPrice = 0) : IStreamQuery<decimal>;

// DTO
public sealed record ItemDto(Guid Id, string Name, bool IsDone = false);
