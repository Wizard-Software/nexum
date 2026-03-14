using Nexum.Abstractions;

namespace Nexum.Testing.Tests;

[Trait("Category", "Unit")]
public sealed class FakeCommandDispatcherTests
{
    [Fact]
    public async Task Setup_Returns_ReturnsConfiguredValueAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>().Returns("hello");

        // Act
        var result = await dispatcher.DispatchAsync(new TestCommand("input"), CancellationToken.None);

        // Assert
        result.Should().Be("hello");
    }

    [Fact]
    public async Task Setup_ReturnsFactory_InvokesFactoryAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>().Returns(cmd => $"echo:{cmd.Value}");

        // Act
        var result = await dispatcher.DispatchAsync(new TestCommand("world"), CancellationToken.None);

        // Assert
        result.Should().Be("echo:world");
    }

    [Fact]
    public async Task Setup_ReturnsAsync_InvokesAsyncFactoryAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>()
            .ReturnsAsync((cmd, ct) => ValueTask.FromResult($"async:{cmd.Value}"));

        // Act
        var result = await dispatcher.DispatchAsync(new TestCommand("test"), CancellationToken.None);

        // Assert
        result.Should().Be("async:test");
    }

    [Fact]
    public async Task Setup_Throws_ThrowsConfiguredExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>().Throws(new InvalidOperationException("boom"));

        // Act
        var act = () => dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None).AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task DispatchAsync_WithoutSetup_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();

        // Act
        var act = () => dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None).AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestCommand*");
    }

    [Fact]
    public async Task DispatchedCommands_TracksAllDispatchedAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>().Returns("r");
        dispatcher.Setup<OtherCommand, int>().Returns(42);

        var c1 = new TestCommand("a");
        var c2 = new OtherCommand(1);
        var c3 = new TestCommand("b");

        // Act
        await dispatcher.DispatchAsync(c1, CancellationToken.None);
        await dispatcher.DispatchAsync(c2, CancellationToken.None);
        await dispatcher.DispatchAsync(c3, CancellationToken.None);

        // Assert
        dispatcher.DispatchedCommands.Should().HaveCount(3);
        dispatcher.DispatchedCommands[0].Should().Be(c1);
        dispatcher.DispatchedCommands[1].Should().Be(c2);
        dispatcher.DispatchedCommands[2].Should().Be(c3);
    }

    [Fact]
    public async Task Reset_ClearsSetupAndHistoryAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<TestCommand, string>().Returns("hello");
        await dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None);

        // Act
        dispatcher.Reset();

        // Assert
        dispatcher.DispatchedCommands.Should().BeEmpty();
        var act = () => dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

internal sealed record TestCommand(string Value) : ICommand<string>;
internal sealed record OtherCommand(int Number) : ICommand<int>;
