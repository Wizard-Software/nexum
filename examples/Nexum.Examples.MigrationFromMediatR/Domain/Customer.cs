namespace Nexum.Examples.MigrationFromMediatR.Domain;

// Domain model — shared by both MediatR and Nexum handlers
public record Customer(Guid Id, string Name, string Email);
