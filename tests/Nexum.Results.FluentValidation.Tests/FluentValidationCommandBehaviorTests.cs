using FluentValidation;
using FluentValidation.Results;
using Nexum.Abstractions;
using Nexum.Results.FluentValidation.Tests.Fixtures;

namespace Nexum.Results.FluentValidation.Tests;

[Trait("Category", "Unit")]
public class FluentValidationCommandBehaviorTests
{
    [Fact]
    public async Task HandleAsync_NoValidators_CallsNextAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("Test", 100m);
        var expectedResult = Result<Guid>.Ok(Guid.NewGuid());
        var validators = Array.Empty<IValidator<CreateOrderCommand>>();
        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>(validators);
        CommandHandlerDelegate<Result<Guid>> next = _ => ValueTask.FromResult(expectedResult);

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_ValidCommand_CallsNextAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("Valid", 10m);
        var expectedResult = Result<Guid>.Ok(Guid.NewGuid());

        var validator = Substitute.For<IValidator<CreateOrderCommand>>();
        validator.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator]);
        CommandHandlerDelegate<Result<Guid>> next = _ => ValueTask.FromResult(expectedResult);

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
        await validator.Received(1).ValidateAsync(command, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_ResultT_ReturnsFailureAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("", -5m);
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required"),
            new("Amount", "Amount must be greater than 0")
        };

        var validator = Substitute.For<IValidator<CreateOrderCommand>>();
        validator.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator]);
        CommandHandlerDelegate<Result<Guid>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationNexumError>();
        var validationError = (ValidationNexumError)result.Error;
        validationError.Code.Should().Be("VALIDATION_FAILED");
        validationError.Message.Should().Be("Name is required; Amount must be greater than 0");
        validationError.Failures.Should().HaveCount(2);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_ResultTError_ReturnsFailureAsync()
    {
        // Arrange
        var command = new TwoParamResultCommand("");
        var failures = new List<ValidationFailure>
        {
            new("Value", "Value is required")
        };

        var validator = Substitute.For<IValidator<TwoParamResultCommand>>();
        validator.ValidateAsync(Arg.Any<TwoParamResultCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var behavior = new FluentValidationCommandBehavior<TwoParamResultCommand, Result<Guid, NexumError>>([validator]);
        CommandHandlerDelegate<Result<Guid, NexumError>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationNexumError>();
        var validationError = (ValidationNexumError)result.Error;
        validationError.Code.Should().Be("VALIDATION_FAILED");
        validationError.Failures.Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_InvalidCommand_NonResultType_ThrowsValidationExceptionAsync()
    {
        // Arrange
        var command = new SimpleCommand("");
        var failures = new List<ValidationFailure>
        {
            new("Value", "Value is required")
        };

        var validator = Substitute.For<IValidator<SimpleCommand>>();
        validator.ValidateAsync(Arg.Any<SimpleCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var behavior = new FluentValidationCommandBehavior<SimpleCommand, Guid>([validator]);
        CommandHandlerDelegate<Guid> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        Func<Task> act = async () => await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Errors.SequenceEqual(failures));
    }

    [Fact]
    public async Task HandleAsync_MultipleValidators_AggregatesFailuresAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("", -5m);

        var validator1 = Substitute.For<IValidator<CreateOrderCommand>>();
        validator1.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required")]));

        var validator2 = Substitute.For<IValidator<CreateOrderCommand>>();
        validator2.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Amount", "Amount must be greater than 0")]));

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator1, validator2]);
        CommandHandlerDelegate<Result<Guid>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        var validationError = (ValidationNexumError)result.Error;
        validationError.Failures.Should().HaveCount(2);
        validationError.Failures.Should().Contain(f => f.PropertyName == "Name");
        validationError.Failures.Should().Contain(f => f.PropertyName == "Amount");
    }

    [Fact]
    public async Task HandleAsync_CancellationTokenCancelled_ThrowsOperationCanceledExceptionAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("Test", 100m);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var validator = Substitute.For<IValidator<CreateOrderCommand>>();
        validator.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns<ValidationResult>(_ => throw new OperationCanceledException());

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator]);
        CommandHandlerDelegate<Result<Guid>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        Func<Task> act = async () => await behavior.HandleAsync(command, next, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task HandleAsync_CustomResultFailureFactory_UsesFactoryAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("", -5m);
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };

        var validator = Substitute.For<IValidator<CreateOrderCommand>>();
        validator.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var customError = new NexumError("CUSTOM", "custom");
        var customResult = Result<Guid>.Fail(customError);

        var factory = Substitute.For<IResultFailureFactory>();
        factory.CanCreate(typeof(Result<Guid>)).Returns(true);
        factory.CreateFailure(typeof(Result<Guid>), Arg.Any<NexumError>())
            .Returns(customResult);

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator], factory);
        CommandHandlerDelegate<Result<Guid>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.Should().Be(customResult);
        factory.Received(1).CanCreate(typeof(Result<Guid>));
        factory.Received(1).CreateFailure(typeof(Result<Guid>), Arg.Any<ValidationNexumError>());
    }

    [Fact]
    public async Task HandleAsync_CustomFactoryCannotCreate_FallsBackToReflectionAsync()
    {
        // Arrange
        var command = new CreateOrderCommand("", -5m);
        var failures = new List<ValidationFailure>
        {
            new("Name", "Name is required")
        };

        var validator = Substitute.For<IValidator<CreateOrderCommand>>();
        validator.ValidateAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var factory = Substitute.For<IResultFailureFactory>();
        factory.CanCreate(typeof(Result<Guid>)).Returns(false);

        var behavior = new FluentValidationCommandBehavior<CreateOrderCommand, Result<Guid>>([validator], factory);
        CommandHandlerDelegate<Result<Guid>> next = _ => throw new InvalidOperationException("Should not be called");

        // Act
        var result = await behavior.HandleAsync(command, next, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationNexumError>();
        factory.Received(1).CanCreate(typeof(Result<Guid>));
        factory.DidNotReceive().CreateFailure(Arg.Any<Type>(), Arg.Any<NexumError>());
    }
}
