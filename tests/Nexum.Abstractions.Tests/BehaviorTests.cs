using System.Runtime.CompilerServices;

namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class BehaviorTests
{
    private record TestCommand(int Value) : ICommand<string>;
    private record TestVoidCommand : IVoidCommand;
    private record TestQuery(Guid Id) : IQuery<int>;
    private record TestStreamQuery : IStreamQuery<string>;

    [Fact]
    public void ICommandBehavior_ConstraintEnforced_TCommandMustBeICommand()
    {
        var behavior = new TestCommandBehavior();

        behavior.Should().NotBeNull();
        behavior.Should().BeAssignableTo<ICommandBehavior<TestCommand, string>>();
    }

    [Fact]
    public async Task ICommandBehavior_HandleAsync_CanInvokeNextAsync()
    {
        var behavior = new TestCommandBehavior();
        var command = new TestCommand(42);
        var nextCalled = false;

        CommandHandlerDelegate<string> next = ct =>
        {
            nextCalled = true;
            return ValueTask.FromResult("handler result");
        };

        var result = await behavior.HandleAsync(command, next, TestContext.Current.CancellationToken);

        result.Should().Be("behavior: handler result");
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ICommandBehavior_VoidCommand_WorksWithUnitAsync()
    {
        var behavior = new TestVoidCommandBehavior();
        var command = new TestVoidCommand();
        var nextCalled = false;

        CommandHandlerDelegate<Unit> next = ct =>
        {
            nextCalled = true;
            return ValueTask.FromResult(default(Unit));
        };

        var result = await behavior.HandleAsync(command, next, TestContext.Current.CancellationToken);

        result.Should().Be(default);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void IQueryBehavior_ConstraintEnforced_TQueryMustBeIQuery()
    {
        var behavior = new TestQueryBehavior();

        behavior.Should().NotBeNull();
        behavior.Should().BeAssignableTo<IQueryBehavior<TestQuery, int>>();
    }

    [Fact]
    public async Task IQueryBehavior_HandleAsync_CanInvokeNextAsync()
    {
        var behavior = new TestQueryBehavior();
        var query = new TestQuery(Guid.NewGuid());
        var nextCalled = false;

        QueryHandlerDelegate<int> next = ct =>
        {
            nextCalled = true;
            return ValueTask.FromResult(100);
        };

        var result = await behavior.HandleAsync(query, next, TestContext.Current.CancellationToken);

        result.Should().Be(200);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void IStreamQueryBehavior_ConstraintEnforced_TQueryMustBeIStreamQuery()
    {
        var behavior = new TestStreamQueryBehavior();

        behavior.Should().NotBeNull();
        behavior.Should().BeAssignableTo<IStreamQueryBehavior<TestStreamQuery, string>>();
    }

    [Fact]
    public async Task IStreamQueryBehavior_HandleAsync_ReturnsIAsyncEnumerableAsync()
    {
        var behavior = new TestStreamQueryBehavior();
        var query = new TestStreamQuery();
        var nextCalled = false;

        StreamQueryHandlerDelegate<string> next = ct =>
        {
            nextCalled = true;
            return YieldItemsAsync(["one", "two"]);
        };

        var stream = behavior.HandleAsync(query, next, TestContext.Current.CancellationToken);
        var results = new List<string>();

        await foreach (var item in stream)
        {
            results.Add(item);
        }

        results.Should().Equal(["prefix:one", "prefix:two"]);
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void CommandHandlerDelegate_IsInvocable_ReturnsValueTask()
    {
        CommandHandlerDelegate<int> del = ct => ValueTask.FromResult(42);

        var task = del(TestContext.Current.CancellationToken);

        task.Should().NotBeNull();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task CommandHandlerDelegate_CanAwait_ReturnsCorrectValueAsync()
    {
        CommandHandlerDelegate<string> del = ct => ValueTask.FromResult("test");

        var result = await del(TestContext.Current.CancellationToken);

        result.Should().Be("test");
    }

    [Fact]
    public void QueryHandlerDelegate_IsInvocable_ReturnsValueTask()
    {
        QueryHandlerDelegate<bool> del = ct => ValueTask.FromResult(true);

        var task = del(TestContext.Current.CancellationToken);

        task.Should().NotBeNull();
        task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task QueryHandlerDelegate_CanAwait_ReturnsCorrectValueAsync()
    {
        QueryHandlerDelegate<int> del = ct => ValueTask.FromResult(99);

        var result = await del(TestContext.Current.CancellationToken);

        result.Should().Be(99);
    }

    [Fact]
    public void StreamQueryHandlerDelegate_IsInvocable_ReturnsIAsyncEnumerable()
    {
        StreamQueryHandlerDelegate<string> del = ct => YieldItemsAsync(["a", "b"]);

        var stream = del(TestContext.Current.CancellationToken);

        stream.Should().NotBeNull();
        stream.Should().BeAssignableTo<IAsyncEnumerable<string>>();
    }

    [Fact]
    public async Task StreamQueryHandlerDelegate_CanIterate_ReturnsCorrectValuesAsync()
    {
        StreamQueryHandlerDelegate<int> del = ct => YieldItemsAsync([1, 2, 3]);

        var results = new List<int>();
        await foreach (var item in del(TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        results.Should().Equal([1, 2, 3]);
    }

    // Helper mock implementations
    private class TestCommandBehavior : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            var result = await next(ct).ConfigureAwait(false);
            return $"behavior: {result}";
        }
    }

    private class TestVoidCommandBehavior : ICommandBehavior<TestVoidCommand, Unit>
    {
        public async ValueTask<Unit> HandleAsync(TestVoidCommand command, CommandHandlerDelegate<Unit> next, CancellationToken ct = default)
        {
            return await next(ct).ConfigureAwait(false);
        }
    }

    private class TestQueryBehavior : IQueryBehavior<TestQuery, int>
    {
        public async ValueTask<int> HandleAsync(TestQuery query, QueryHandlerDelegate<int> next, CancellationToken ct = default)
        {
            var result = await next(ct).ConfigureAwait(false);
            return result * 2;
        }
    }

    private class TestStreamQueryBehavior : IStreamQueryBehavior<TestStreamQuery, string>
    {
        public async IAsyncEnumerable<string> HandleAsync(TestStreamQuery query, StreamQueryHandlerDelegate<string> next, [EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var item in next(ct).ConfigureAwait(false))
            {
                yield return $"prefix:{item}";
            }
        }
    }

    private static async IAsyncEnumerable<T> YieldItemsAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }
}
