using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Results;
using Nexum.Abstractions;
using Nexum.Results.FluentValidation.Internal;

namespace Nexum.Results.FluentValidation;

/// <summary>
/// Pipeline behavior that validates commands using FluentValidation before handler execution.
/// When validation fails and TResult is a Result type, returns <c>Result.Fail</c> with
/// <see cref="ValidationNexumError"/>. For non-Result types, throws <see cref="ValidationException"/>.
/// </summary>
/// <typeparam name="TCommand">The command type being validated.</typeparam>
/// <typeparam name="TResult">The result type of the command.</typeparam>
[BehaviorOrder(0)]
public sealed class FluentValidationCommandBehavior<TCommand, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TResult>
    : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private readonly IValidator<TCommand>[] _validators;
    private readonly IResultFailureFactory? _failureFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FluentValidationCommandBehavior{TCommand, TResult}"/> class.
    /// </summary>
    /// <param name="validators">The validators registered for this command type.</param>
    /// <param name="failureFactory">Optional factory for creating failure results. If null, uses cached reflection fallback.</param>
    public FluentValidationCommandBehavior(
        IEnumerable<IValidator<TCommand>> validators,
        IResultFailureFactory? failureFactory = null)
    {
        _validators = validators as IValidator<TCommand>[] ?? [.. validators];
        _failureFactory = failureFactory;
    }

    /// <inheritdoc />
    public async ValueTask<TResult> HandleAsync(
        TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct = default)
    {
        if (_validators.Length == 0)
        {
            return await next(ct).ConfigureAwait(false);
        }

        var failures = new List<ValidationFailure>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(command, ct).ConfigureAwait(false);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count == 0)
        {
            return await next(ct).ConfigureAwait(false);
        }

        var error = new ValidationNexumError(
            "VALIDATION_FAILED",
            string.Join("; ", failures.Select(f => f.ErrorMessage)),
            failures);

        if (_failureFactory?.CanCreate(typeof(TResult)) == true)
        {
            return (TResult)_failureFactory.CreateFailure(typeof(TResult), error);
        }

        if (ReflectionResultFailureFactory<TResult>.CanCreate)
        {
            return ReflectionResultFailureFactory<TResult>.Create(error);
        }

        throw new ValidationException(failures);
    }
}
