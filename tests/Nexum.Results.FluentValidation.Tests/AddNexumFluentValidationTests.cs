#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode warning for test usage

using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection;
using Nexum.Results.FluentValidation.Tests.Fixtures;

namespace Nexum.Results.FluentValidation.Tests;

[Trait("Category", "Integration")]
public class AddNexumFluentValidationTests
{
    [Fact]
    public void AddNexumFluentValidation_RegistersBehaviorAndFactory()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNexumFluentValidation();
        var sp = services.BuildServiceProvider();

        // Assert
        var factory = sp.GetService<IResultFailureFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public void AddNexumFluentValidation_WithScopedValidatorLifetime_RegistersValidatorsAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNexumFluentValidation(
            validatorLifetime: ServiceLifetime.Scoped,
            assemblies: [typeof(CreateOrderCommandValidator).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IValidator<CreateOrderCommand>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNexumFluentValidation_WithTransientValidatorLifetime_RegistersValidatorsAsTransient()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNexumFluentValidation(
            validatorLifetime: ServiceLifetime.Transient,
            assemblies: [typeof(CreateOrderCommandValidator).Assembly]);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IValidator<CreateOrderCommand>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddNexumFluentValidation_WithCustomBehaviorOrder_RegistersBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNexumFluentValidation(behaviorOrder: 5);

        // Assert
        var behaviorDescriptor = services.FirstOrDefault(d =>
            d.ServiceType.IsGenericType &&
            d.ServiceType.GetGenericTypeDefinition() == typeof(ICommandBehavior<,>));
        behaviorDescriptor.Should().NotBeNull();
    }

    [Fact]
    public async Task AddNexumFluentValidation_FullPipeline_ValidCommand_ReturnsSuccessAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(CreateOrderCommandHandler).Assembly]);
        services.AddNexumFluentValidation(assemblies: [typeof(CreateOrderCommandValidator).Assembly]);

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var command = new CreateOrderCommand("Valid", 10m);

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AddNexumFluentValidation_FullPipeline_InvalidCommand_ReturnsFailureAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(CreateOrderCommandHandler).Assembly]);
        services.AddNexumFluentValidation(assemblies: [typeof(CreateOrderCommandValidator).Assembly]);

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var command = new CreateOrderCommand("", -5m);

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeOfType<ValidationNexumError>();
        var validationError = (ValidationNexumError)result.Error;
        validationError.Code.Should().Be("VALIDATION_FAILED");
        validationError.Failures.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddNexumFluentValidation_FullPipeline_PartiallyInvalidCommand_ReturnsFailureAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(CreateOrderCommandHandler).Assembly]);
        services.AddNexumFluentValidation(assemblies: [typeof(CreateOrderCommandValidator).Assembly]);

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var command = new CreateOrderCommand("Valid", -5m);

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        var validationError = (ValidationNexumError)result.Error;
        validationError.Failures.Should().HaveCount(1);
        validationError.Failures[0].PropertyName.Should().Be("Amount");
    }

    [Fact]
    public async Task AddNexumFluentValidation_NoValidatorsRegistered_CommandSucceedsAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(CreateOrderCommandHandler).Assembly]);
        services.AddNexumFluentValidation(); // No assemblies = no validators

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var command = new CreateOrderCommand("", -5m);

        // Act
        var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
