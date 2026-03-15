using Nexum.Abstractions;

namespace Nexum.Examples.MigrationFromMediatR.MediatR;

// MIGRATION STEP 1: Add Nexum interface alongside existing MediatR interface.
// The record now satisfies both dispatchers simultaneously.
// Both MediatR's ISender and Nexum's ICommandDispatcher can handle this request —
// the handler registration (MediatR IRequestHandler) is bridged to Nexum via
// MediatRCommandAdapter registered by AddNexumWithMediatRCompat().
//
// BEFORE: public record CreateCustomerRequest(...) : MediatR.IRequest<Guid>
// AFTER:  public record CreateCustomerRequest(...) : MediatR.IRequest<Guid>, ICommand<Guid>
public record CreateCustomerRequest(string Name, string Email)
    : global::MediatR.IRequest<Guid>, // MediatR interface (existing)
      ICommand<Guid>;                 // Nexum interface (added for migration)
