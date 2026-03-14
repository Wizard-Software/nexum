using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "Unit")]
public class MediatRQueryAdapterTests
{
    [Fact]
    public async Task HandleAsync_DelegatesToMediatRHandler_ReturnsResultAsync()
    {
        // Arrange
        var expectedName = "Test Result";
        var mediatRHandler = Substitute.For<global::MediatR.IRequestHandler<TestQuery, string>>();
        mediatRHandler.Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedName));

        var adapter = new MediatRQueryAdapter<TestQuery, string>(mediatRHandler);

        // Act
        var result = await adapter.HandleAsync(new TestQuery(42), CancellationToken.None);

        // Assert
        result.Should().Be(expectedName);
        await mediatRHandler.Received(1).Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_PropagatesCancellationTokenAsync()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var mediatRHandler = Substitute.For<global::MediatR.IRequestHandler<TestQuery, string>>();
        mediatRHandler.Handle(Arg.Any<TestQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("result"));

        var adapter = new MediatRQueryAdapter<TestQuery, string>(mediatRHandler);

        // Act
        await adapter.HandleAsync(new TestQuery(1), token);

        // Assert
        await mediatRHandler.Received(1).Handle(Arg.Any<TestQuery>(), token);
    }

    public record TestQuery(int Id) : IQuery<string>, global::MediatR.IRequest<string>;
}
