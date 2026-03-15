using Nexum.Examples.MigrationFromMediatR.Shared;

namespace Nexum.Examples.MigrationFromMediatR.MediatR;

// BEFORE (MediatR handler): This handler implements MediatR's IRequestHandler.
// During migration, it continues to work — AddNexumWithMediatRCompat() registers
// a MediatRCommandAdapter that bridges this handler to Nexum's ICommandDispatcher.
public sealed class CreateCustomerRequestHandler(CustomerStore store)
    : global::MediatR.IRequestHandler<CreateCustomerRequest, Guid>
{
    public Task<Guid> Handle(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var id = store.Add(request.Name, request.Email);
        Console.WriteLine($"  [MediatR Handler] Created customer: {request.Name} ({request.Email}) → Id={id}");
        return Task.FromResult(id);
    }
}
