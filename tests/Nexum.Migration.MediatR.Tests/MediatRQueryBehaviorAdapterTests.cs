using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Unit")]
public class MediatRQueryBehaviorAdapterTests
{
    [Fact]
    public async Task HandleAsync_InvokesNextDelegateAsync()
    {
        // Arrange
        var expectedResult = "test result";
        var mediatRBehavior = Substitute.For<global::MediatR.IPipelineBehavior<TestQuery, string>>();
        mediatRBehavior.Handle(
                Arg.Any<TestQuery>(),
                Arg.Any<global::MediatR.RequestHandlerDelegate<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var next = callInfo.Arg<global::MediatR.RequestHandlerDelegate<string>>();
                return next();
            });

        var adapter = new MediatRQueryBehaviorAdapter<TestQuery, string>(mediatRBehavior);
        QueryHandlerDelegate<string> nexumNext = _ => new ValueTask<string>(expectedResult);

        // Act
        var result = await adapter.HandleAsync(new TestQuery(1), nexumNext, CancellationToken.None);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void Constructor_NullBehavior_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new MediatRQueryBehaviorAdapter<TestQuery, string>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediatRBehavior");
    }

    public record TestQuery(int Id) : IQuery<string>, global::MediatR.IRequest<string>;
}
