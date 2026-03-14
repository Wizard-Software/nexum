using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Unit")]
public class MediatRCommandBehaviorAdapterTests
{
    [Fact]
    public async Task HandleAsync_InvokesNextDelegateAsync()
    {
        // Arrange
        var expectedResult = Guid.NewGuid();
        var mediatRBehavior = Substitute.For<global::MediatR.IPipelineBehavior<TestCommand, Guid>>();
        mediatRBehavior.Handle(
                Arg.Any<TestCommand>(),
                Arg.Any<global::MediatR.RequestHandlerDelegate<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var next = callInfo.Arg<global::MediatR.RequestHandlerDelegate<Guid>>();
                return next();
            });

        var adapter = new MediatRCommandBehaviorAdapter<TestCommand, Guid>(mediatRBehavior);
        CommandHandlerDelegate<Guid> nexumNext = _ => new ValueTask<Guid>(expectedResult);

        // Act
        var result = await adapter.HandleAsync(new TestCommand("test"), nexumNext, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_ConvertsNexumDelegateToMediatRDelegateAsync()
    {
        // Arrange
        global::MediatR.RequestHandlerDelegate<Guid>? capturedDelegate = null;
        var mediatRBehavior = Substitute.For<global::MediatR.IPipelineBehavior<TestCommand, Guid>>();
        mediatRBehavior.Handle(
                Arg.Any<TestCommand>(),
                Arg.Any<global::MediatR.RequestHandlerDelegate<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedDelegate = callInfo.Arg<global::MediatR.RequestHandlerDelegate<Guid>>();
                return capturedDelegate();
            });

        var expectedResult = Guid.NewGuid();
        var adapter = new MediatRCommandBehaviorAdapter<TestCommand, Guid>(mediatRBehavior);
        CommandHandlerDelegate<Guid> nexumNext = _ => new ValueTask<Guid>(expectedResult);

        // Act
        var result = await adapter.HandleAsync(new TestCommand("test"), nexumNext, CancellationToken.None);

        // Assert
        capturedDelegate.Should().NotBeNull();
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task HandleAsync_PassesCancellationTokenToNextAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        CancellationToken capturedToken = default;

        var mediatRBehavior = Substitute.For<global::MediatR.IPipelineBehavior<TestCommand, Guid>>();
        mediatRBehavior.Handle(
                Arg.Any<TestCommand>(),
                Arg.Any<global::MediatR.RequestHandlerDelegate<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var next = callInfo.Arg<global::MediatR.RequestHandlerDelegate<Guid>>();
                return next();
            });

        var adapter = new MediatRCommandBehaviorAdapter<TestCommand, Guid>(mediatRBehavior);
        CommandHandlerDelegate<Guid> nexumNext = ct =>
        {
            capturedToken = ct;
            return new ValueTask<Guid>(Guid.NewGuid());
        };

        // Act
        await adapter.HandleAsync(new TestCommand("test"), nexumNext, token);

        // Assert
        capturedToken.Should().Be(token);
    }

    [Fact]
    public async Task HandleAsync_WhenBehaviorShortCircuits_DoesNotCallNextAsync()
    {
        // Arrange
        var shortCircuitResult = Guid.NewGuid();
        var mediatRBehavior = Substitute.For<global::MediatR.IPipelineBehavior<TestCommand, Guid>>();
        mediatRBehavior.Handle(
                Arg.Any<TestCommand>(),
                Arg.Any<global::MediatR.RequestHandlerDelegate<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(shortCircuitResult));

        var nextCalled = false;
        var adapter = new MediatRCommandBehaviorAdapter<TestCommand, Guid>(mediatRBehavior);
        CommandHandlerDelegate<Guid> nexumNext = _ =>
        {
            nextCalled = true;
            return new ValueTask<Guid>(Guid.NewGuid());
        };

        // Act
        var result = await adapter.HandleAsync(new TestCommand("test"), nexumNext, CancellationToken.None);

        // Assert
        result.Should().Be(shortCircuitResult);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullBehavior_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MediatRCommandBehaviorAdapter<TestCommand, Guid>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediatRBehavior");
    }

    public record TestCommand(string Name) : ICommand<Guid>, global::MediatR.IRequest<Guid>;
}
