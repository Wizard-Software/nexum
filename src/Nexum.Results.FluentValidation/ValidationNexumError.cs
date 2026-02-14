using FluentValidation.Results;

namespace Nexum.Results.FluentValidation;

/// <summary>
/// Represents a validation error containing individual validation failures
/// from FluentValidation. Extends <see cref="NexumError"/> for seamless
/// integration with the Nexum Result Pattern.
/// </summary>
/// <param name="Code">A machine-readable error code (default: "VALIDATION_FAILED").</param>
/// <param name="Message">A human-readable aggregation of validation failure messages.</param>
/// <param name="Failures">The individual validation failures from FluentValidation.</param>
public sealed record ValidationNexumError(
    string Code,
    string Message,
    IReadOnlyList<ValidationFailure> Failures)
    : NexumError(Code, Message);
