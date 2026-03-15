using Nexum.Abstractions;
using Nexum.Examples.MigrationFromMediatR.Domain;

namespace Nexum.Examples.MigrationFromMediatR.MediatR;

// MIGRATION STEP 1: Add Nexum interface alongside existing MediatR interface.
// Same dual-interface pattern as CreateCustomerRequest — allows gradual migration.
//
// BEFORE: public record GetCustomerRequest(Guid Id) : MediatR.IRequest<Customer?>
// AFTER:  public record GetCustomerRequest(Guid Id) : MediatR.IRequest<Customer?>, IQuery<Customer?>
public record GetCustomerRequest(Guid Id)
    : global::MediatR.IRequest<Customer?>, // MediatR interface (existing)
      IQuery<Customer?>;                   // Nexum interface (added for migration)
