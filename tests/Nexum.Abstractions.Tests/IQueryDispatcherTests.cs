using System.Runtime.CompilerServices;

namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class IQueryDispatcherTests
{
    private record TestQuery(Guid Id) : IQuery<string>;
    private record TestStreamQuery(int Limit) : IStreamQuery<int>;

    private class MockQueryDispatcher : IQueryDispatcher
    {
        public ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
            => ValueTask.FromResult<TResult>(default!);

        public async IAsyncEnumerable<TResult> StreamAsync<TResult>(
            IStreamQuery<TResult> query,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield return default!;
        }
    }

    [Fact]
    public void MockImplementation_SatisfiesInterface()
    {
        var dispatcher = new MockQueryDispatcher();

        dispatcher.Should().BeAssignableTo<IQueryDispatcher>();
    }

    [Fact]
    public async Task DispatchAsync_WithQuery_ReturnsResultAsync()
    {
        var dispatcher = new MockQueryDispatcher();
        var query = new TestQuery(Guid.NewGuid());

        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task StreamAsync_WithStreamQuery_ReturnsAsyncEnumerableAsync()
    {
        var dispatcher = new MockQueryDispatcher();
        var query = new TestStreamQuery(10);

        var stream = dispatcher.StreamAsync(query, TestContext.Current.CancellationToken);

        stream.Should().NotBeNull();
        var result = await stream.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        result.Should().Be(0);
    }

    [Fact]
    public async Task StreamAsync_CanIterateItemsAsync()
    {
        var dispatcher = new MockQueryDispatcher();
        var query = new TestStreamQuery(10);

        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            item.Should().Be(0);
            break;
        }
    }

    [Fact]
    public void DispatchAsync_ReturnsValueTask()
    {
        var dispatcher = new MockQueryDispatcher();
        var query = new TestQuery(Guid.NewGuid());

        var result = dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void StreamAsync_ReturnsIAsyncEnumerable()
    {
        var dispatcher = new MockQueryDispatcher();
        var query = new TestStreamQuery(10);

        var stream = dispatcher.StreamAsync(query, TestContext.Current.CancellationToken);

        stream.Should().NotBeNull();
        stream.Should().BeAssignableTo<IAsyncEnumerable<int>>();
    }
}
