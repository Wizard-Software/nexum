using Nexum.Abstractions;

namespace Nexum.Examples.MigrationFromMediatR.Nexum;

// AFTER (native Nexum command): New functionality written directly with Nexum.
// Uses IVoidCommand — no MediatR dependency needed for new code.
// This is the target state: all new commands use Nexum interfaces natively.
public record DeleteCustomerCommand(Guid Id) : IVoidCommand;
