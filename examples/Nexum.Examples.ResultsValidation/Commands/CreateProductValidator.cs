using FluentValidation;

namespace Nexum.Examples.ResultsValidation.Commands;

// FluentValidation validator — discovered automatically by AddNexumFluentValidation()
// when the assembly is scanned. Runs as a pipeline behavior before the handler.
public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage("Product name must not be empty.");

        RuleFor(c => c.Price)
            .GreaterThan(0)
            .WithMessage("Product price must be greater than zero.");
    }
}
