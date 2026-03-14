using Nexum.Abstractions;

namespace Nexum.Testing.Tests;

[Trait("Category", "Unit")]
public sealed class FakeQueryDispatcherTests
{
    [Fact]
    public async Task Setup_Returns_ReturnsConfiguredValueAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<TestQuery, string>().Returns("result");

        // Act
        var result = await dispatcher.DispatchAsync(new TestQuery("filter"), CancellationToken.None);

        // Assert
        result.Should().Be("result");
    }

    [Fact]
    public async Task DispatchAsync_WithoutSetup_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();

        // Act
        var act = () => dispatcher.DispatchAsync(new TestQuery("x"), CancellationToken.None).AsTask();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestQuery*");
    }

    [Fact]
    public async Task DispatchedQueries_TracksAllDispatchedAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<TestQuery, string>().Returns("r");

        var q1 = new TestQuery("a");
        var q2 = new TestQuery("b");
        var q3 = new TestQuery("c");

        // Act
        await dispatcher.DispatchAsync(q1, CancellationToken.None);
        await dispatcher.DispatchAsync(q2, CancellationToken.None);
        await dispatcher.DispatchAsync(q3, CancellationToken.None);

        // Assert
        dispatcher.DispatchedQueries.Should().HaveCount(3);
        dispatcher.DispatchedQueries[0].Should().Be(q1);
        dispatcher.DispatchedQueries[1].Should().Be(q2);
        dispatcher.DispatchedQueries[2].Should().Be(q3);
    }

    [Fact]
    public async Task Setup_Throws_ThrowsConfiguredExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<TestQuery, string>().Throws(new ArgumentException("bad query"));

        // Act
        var act = () => dispatcher.DispatchAsync(new TestQuery("x"), CancellationToken.None).AsTask();

        // Assert
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("bad query");
    }

    [Fact]
    public async Task StreamAsync_WithSetup_ReturnsConfiguredStreamAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.SetupStream<TestStreamQuery, int>().Returns(1, 2, 3);

        // Act
        var results = new List<int>();
        await foreach (var item in dispatcher.StreamAsync(new TestStreamQuery(3), CancellationToken.None))
        {
            results.Add(item);
        }

        // Assert
        results.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task StreamAsync_WithoutSetup_ThrowsInvalidOperationExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();

        // Act & Assert — the exception should be thrown when getting the enumerator / first MoveNextAsync
        var act = async () =>
        {
            await foreach (var _ in dispatcher.StreamAsync(new TestStreamQuery(1), CancellationToken.None))
            {
                // not reached
            }
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TestStreamQuery*");
    }

    [Fact]
    public async Task Reset_ClearsSetupAndHistoryAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<TestQuery, string>().Returns("hello");
        await dispatcher.DispatchAsync(new TestQuery("x"), CancellationToken.None);

        // Act
        dispatcher.Reset();

        // Assert
        dispatcher.DispatchedQueries.Should().BeEmpty();
        var act = () => dispatcher.DispatchAsync(new TestQuery("x"), CancellationToken.None).AsTask();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

internal sealed record TestQuery(string Filter) : IQuery<string>;
internal sealed record TestStreamQuery(int Count) : IStreamQuery<int>;
