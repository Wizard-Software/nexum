using MediatRNS = global::MediatR;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Unit")]
public class MediatRNotificationAdapterTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToMediatRNotificationHandlerAsync()
    {
        // Arrange
        var mediatRHandler = Substitute.For<MediatRNS.INotificationHandler<TestNotification>>();
        mediatRHandler.Handle(Arg.Any<TestNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var adapter = new MediatRNotificationAdapter<TestNotification>(mediatRHandler);

        // Act
        await adapter.HandleAsync(new TestNotification("test"), CancellationToken.None);

        // Assert
        await mediatRHandler.Received(1).Handle(Arg.Any<TestNotification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mediatRHandler = Substitute.For<MediatRNS.INotificationHandler<TestNotification>>();
        mediatRHandler.Handle(Arg.Any<TestNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var adapter = new MediatRNotificationAdapter<TestNotification>(mediatRHandler);

        // Act
        await adapter.HandleAsync(new TestNotification("test"), token);

        // Assert
        await mediatRHandler.Received(1).Handle(Arg.Any<TestNotification>(), token);
    }

    [Fact]
    public void Constructor_NullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MediatRNotificationAdapter<TestNotification>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediatRHandler");
    }

    [Fact]
    public async Task HandleAsync_WhenMediatRHandlerThrows_PropagatesExceptionAsync()
    {
        // Arrange
        var mediatRHandler = Substitute.For<MediatRNS.INotificationHandler<TestNotification>>();
        mediatRHandler.Handle(Arg.Any<TestNotification>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("MediatR error")));

        var adapter = new MediatRNotificationAdapter<TestNotification>(mediatRHandler);

        // Act & Assert
        var act = async () => await adapter.HandleAsync(new TestNotification("test"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("MediatR error");
    }

    public record TestNotification(string Message) : Nexum.Abstractions.INotification, MediatRNS.INotification;
}
