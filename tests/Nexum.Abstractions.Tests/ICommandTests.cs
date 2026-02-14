namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class ICommandTests
{
    // Test helpers
    private record CreateOrder(string Product) : ICommand<Guid>;
    private record DeleteOrder(Guid Id) : IVoidCommand;

    // Covariance test helpers
    private class Animal;
    private class Dog : Animal;
    private record GetAnimalCommand : ICommand<Dog>;

    [Fact]
    public void Command_ImplementingICommandOfT_IsICommand()
    {
        var command = new CreateOrder("Widget");

        command.Should().BeAssignableTo<ICommand>();
        command.Should().BeAssignableTo<ICommand<Guid>>();
    }

    [Fact]
    public void ICommandOfT_IsCovariant()
    {
        ICommand<Dog> dogCommand = new GetAnimalCommand();
        ICommand<Animal> animalCommand = dogCommand; // covariance

        animalCommand.Should().NotBeNull();
        animalCommand.Should().BeAssignableTo<ICommand<Animal>>();
        animalCommand.Should().BeAssignableTo<ICommand>();
    }

    [Fact]
    public void VoidCommand_IsICommandOfUnit()
    {
        var command = new DeleteOrder(Guid.NewGuid());

        command.Should().BeAssignableTo<IVoidCommand>();
        command.Should().BeAssignableTo<ICommand<Unit>>();
        command.Should().BeAssignableTo<ICommand>();
    }

    [Fact]
    public void VoidCommand_CanBePassedAsICommand()
    {
        ICommand command = new DeleteOrder(Guid.NewGuid());

        command.Should().BeAssignableTo<ICommand>();
    }

    [Fact]
    public void ICommandHandler_ConstraintEnforced_TCommandMustImplementICommandOfTResult()
    {
        var handler = new TestCommandHandler();

        handler.Should().BeAssignableTo<ICommandHandler<CreateOrder, Guid>>();
    }

    [Fact]
    public void ICommandOfT_MultiLevelCovariance_AssignsCorrectly()
    {
        ICommand<Dog> dogCommand = new GetAnimalCommand();
        ICommand<Animal> animalCommand = dogCommand; // first level
        ICommand<object> objectCommand = animalCommand; // second level

        objectCommand.Should().NotBeNull();
        objectCommand.Should().BeAssignableTo<ICommand<object>>();
        objectCommand.Should().BeAssignableTo<ICommand>();
    }

    [Fact]
    public async Task VoidCommand_HandlerReturnsUnitAsync()
    {
        var handler = new TestVoidCommandHandler();
        var command = new DeleteOrder(Guid.NewGuid());

        var result = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        result.Should().Be(Unit.Value);
    }

    // Test handler implementations
    private class TestCommandHandler : ICommandHandler<CreateOrder, Guid>
    {
        public ValueTask<Guid> HandleAsync(CreateOrder command, CancellationToken ct = default)
            => ValueTask.FromResult(Guid.NewGuid());
    }

    private class TestVoidCommandHandler : ICommandHandler<DeleteOrder, Unit>
    {
        public ValueTask<Unit> HandleAsync(DeleteOrder command, CancellationToken ct = default)
            => ValueTask.FromResult(Unit.Value);
    }
}
