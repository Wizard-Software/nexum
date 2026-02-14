namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class ICommandDispatcherTests
{
    private record TestCommand(string Name) : ICommand<int>;

    private class MockCommandDispatcher : ICommandDispatcher
    {
        public ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
            => ValueTask.FromResult<TResult>(default!);
    }

    [Fact]
    public void MockImplementation_SatisfiesInterface()
    {
        var dispatcher = new MockCommandDispatcher();

        dispatcher.Should().BeAssignableTo<ICommandDispatcher>();
    }

    [Fact]
    public async Task DispatchAsync_WithCommand_ReturnsResultAsync()
    {
        var dispatcher = new MockCommandDispatcher();
        var command = new TestCommand("test");

        var result = await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        result.Should().Be(0);
    }

    [Fact]
    public void DispatchAsync_ReturnsValueTask()
    {
        var dispatcher = new MockCommandDispatcher();
        var command = new TestCommand("test");

        var result = dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        result.IsCompletedSuccessfully.Should().BeTrue();
    }
}
