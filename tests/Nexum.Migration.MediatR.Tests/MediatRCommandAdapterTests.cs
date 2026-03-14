using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Unit")]
public class MediatRCommandAdapterTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToMediatRHandler_ReturnsResultAsync()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var mediatRHandler = Substitute.For<global::MediatR.IRequestHandler<TestCommand, Guid>>();
        mediatRHandler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedId));

        var adapter = new MediatRCommandAdapter<TestCommand, Guid>(mediatRHandler);

        // Act
        var result = await adapter.HandleAsync(new TestCommand("test"), CancellationToken.None);

        // Assert
        result.Should().Be(expectedId);
        await mediatRHandler.Received(1).Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mediatRHandler = Substitute.For<global::MediatR.IRequestHandler<TestCommand, Guid>>();
        mediatRHandler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var adapter = new MediatRCommandAdapter<TestCommand, Guid>(mediatRHandler);

        // Act
        await adapter.HandleAsync(new TestCommand("test"), token);

        // Assert
        await mediatRHandler.Received(1).Handle(Arg.Any<TestCommand>(), token);
    }

    [Fact]
    public void Constructor_NullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MediatRCommandAdapter<TestCommand, Guid>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediatRHandler");
    }

    [Fact]
    public async Task HandleAsync_WhenMediatRHandlerThrows_PropagatesExceptionAsync()
    {
        // Arrange
        var mediatRHandler = Substitute.For<global::MediatR.IRequestHandler<TestCommand, Guid>>();
        mediatRHandler.Handle(Arg.Any<TestCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Guid>(new InvalidOperationException("MediatR error")));

        var adapter = new MediatRCommandAdapter<TestCommand, Guid>(mediatRHandler);

        // Act & Assert
        var act = async () => await adapter.HandleAsync(new TestCommand("test"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("MediatR error");
    }

    public record TestCommand(string Name) : ICommand<Guid>, global::MediatR.IRequest<Guid>;
}
