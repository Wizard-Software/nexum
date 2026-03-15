using Nexum.Abstractions;
using Nexum.Results;

namespace Nexum.Examples.ResultsValidation.Commands;

// Handler only runs when validation has already passed (behavior runs first).
// Manual guard added as defence-in-depth example.
public sealed class CreateProductHandler : ICommandHandler<CreateProductCommand, Result<Guid>>
{
    public ValueTask<Result<Guid>> HandleAsync(CreateProductCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            var error = new NexumError("INVALID_NAME", "Product name must not be empty.");
            return ValueTask.FromResult(Result<Guid>.Fail(error));
        }

        if (command.Price <= 0)
        {
            var error = new NexumError("INVALID_PRICE", "Product price must be greater than zero.");
            return ValueTask.FromResult(Result<Guid>.Fail(error));
        }

        var id = Guid.NewGuid();
        Console.WriteLine($"  Product created: {command.Name} @ {command.Price:C} (Id: {id})");
        return ValueTask.FromResult(Result<Guid>.Ok(id));
    }
}
