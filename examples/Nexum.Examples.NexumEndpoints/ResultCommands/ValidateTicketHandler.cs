using Nexum.Abstractions;
using Nexum.Examples.NexumEndpoints.Domain;
using Nexum.Results;

namespace Nexum.Examples.NexumEndpoints.ResultCommands;

// Demonstrates manual Result<T> validation (without FluentValidation).
// The runtime path uses WithNexumResultMapping() to map Result<T> → HTTP:
//   IsSuccess  → 200 OK with value body
//   !IsSuccess → ProblemDetails (RFC 9457) with error code + message
[CommandHandler]
public sealed class ValidateTicketHandler : ICommandHandler<ValidateTicketCommand, Result<Ticket>>
{
    public ValueTask<Result<Ticket>> HandleAsync(ValidateTicketCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            // Result.Fail → mapped to HTTP 400 ProblemDetails by NexumResultEndpointFilter
            return ValueTask.FromResult(
                Result<Ticket>.Fail(new NexumError("Validation", "Title is required")));
        }

        var ticket = new Ticket(Guid.NewGuid(), command.Title, "Open");

        // Result.Ok → mapped to HTTP 200 OK with Ticket body
        return ValueTask.FromResult(Result<Ticket>.Ok(ticket));
    }
}
