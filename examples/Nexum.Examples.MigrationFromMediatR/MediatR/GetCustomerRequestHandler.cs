using Nexum.Examples.MigrationFromMediatR.Domain;
using Nexum.Examples.MigrationFromMediatR.Shared;

namespace Nexum.Examples.MigrationFromMediatR.MediatR;

// BEFORE (MediatR handler): Implements MediatR's IRequestHandler for queries.
// Bridged to Nexum's IQueryDispatcher via MediatRQueryAdapter.
public sealed class GetCustomerRequestHandler(CustomerStore store)
    : global::MediatR.IRequestHandler<GetCustomerRequest, Customer?>
{
    public Task<Customer?> Handle(GetCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = store.GetById(request.Id);
        Console.WriteLine(customer is not null
            ? $"  [MediatR Handler] Found customer: {customer.Name}"
            : $"  [MediatR Handler] Customer not found: {request.Id}");
        return Task.FromResult(customer);
    }
}
