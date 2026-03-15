namespace Nexum.Examples.WebApi.Domain;

/// <summary>Represents an order in the system.</summary>
public sealed record Order(Guid Id, string Product, int Quantity, decimal Total);
