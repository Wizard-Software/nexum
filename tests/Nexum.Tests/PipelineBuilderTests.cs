using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class PipelineBuilderTests
{
    [Fact]
    public async Task BuildCommandPipeline_NoBehaviors_ReturnsHandlerOnlyAsync()
    {
        // Arrange
        var executionLog = new List<string>();
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();

        var command = new TestCommand("test");
        var handler = new TestCommandHandler(executionLog);

        // Act
        var pipeline = PipelineBuilder.BuildCommandPipeline<TestCommand, string>(sp, command, handler, new NexumOptions());
        var result = await pipeline(CancellationToken.None);

        // Assert
        result.Should().Be("handled:test");
        executionLog.Should().ContainInOrder("Handler");
        executionLog.Count.Should().Be(1);
    }

    [Fact]
    public async Task BuildCommandPipeline_OneBehavior_WrapsHandlerAsync()
    {
        // Arrange
        var executionLog = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorA(executionLog));
        var sp = services.BuildServiceProvider();

        var command = new TestCommand("test");
        var handler = new TestCommandHandler(executionLog);

        // Act
        var pipeline = PipelineBuilder.BuildCommandPipeline<TestCommand, string>(sp, command, handler, new NexumOptions());
        var result = await pipeline(CancellationToken.None);

        // Assert
        result.Should().Be("handled:test");
        executionLog.Should().ContainInOrder("BehaviorA:Before", "Handler", "BehaviorA:After");
        executionLog.Count.Should().Be(3);
    }

    [Fact]
    public async Task BuildCommandPipeline_ThreeBehaviors_ExecutesInOrderAsync()
    {
        // Arrange
        var executionLog = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorA(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorB(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorC(executionLog));
        var sp = services.BuildServiceProvider();

        var command = new TestCommand("test");
        var handler = new TestCommandHandler(executionLog);

        // Act
        var pipeline = PipelineBuilder.BuildCommandPipeline<TestCommand, string>(sp, command, handler, new NexumOptions());
        var result = await pipeline(CancellationToken.None);

        // Assert
        result.Should().Be("handled:test");
        executionLog.Should().ContainInOrder(
            "BehaviorA:Before",
            "BehaviorB:Before",
            "BehaviorC:Before",
            "Handler",
            "BehaviorC:After",
            "BehaviorB:After",
            "BehaviorA:After"
        );
        executionLog.Count.Should().Be(7);
    }

    [Fact]
    public async Task BuildCommandPipeline_MixedOrdering_RespectsAttributeAsync()
    {
        // Arrange
        var executionLog = new List<string>();
        var services = new ServiceCollection();
        // Register in non-sorted order: Order=10, Order=1, Order=0 (default/no attr)
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorWithOrder10(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorWithOrder1(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorA(executionLog)); // no attr = order 0
        var sp = services.BuildServiceProvider();

        var command = new TestCommand("test");
        var handler = new TestCommandHandler(executionLog);

        // Act
        var pipeline = PipelineBuilder.BuildCommandPipeline<TestCommand, string>(sp, command, handler, new NexumOptions());
        var result = await pipeline(CancellationToken.None);

        // Assert
        result.Should().Be("handled:test");
        // Expected order: BehaviorA (0), Order1 (1), Order10 (10)
        executionLog.Should().ContainInOrder(
            "BehaviorA:Before",
            "Order1:Before",
            "Order10:Before",
            "Handler",
            "Order10:After",
            "Order1:After",
            "BehaviorA:After"
        );
        executionLog.Count.Should().Be(7);
    }

    [Fact]
    public async Task BuildCommandPipeline_MixedOrderedAndUnordered_SortsCorrectlyAsync()
    {
        // Arrange — multiple behaviors with same order to verify stable sort (insertion order preserved)
        // Registration order: NoOrderA(0), Order5(5), NoOrderB(0), Order1(1)
        // Expected sort: NoOrderA(0), NoOrderB(0), Order1(1), Order5(5) — stable sort preserves insertion within same order
        var executionLog = new List<string>();
        var services = new ServiceCollection();
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorNoOrderA(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorWithOrder5(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorNoOrderB(executionLog));
        services.AddTransient<ICommandBehavior<TestCommand, string>>(_ => new TestBehaviorWithOrder1Stable(executionLog));
        var sp = services.BuildServiceProvider();

        var command = new TestCommand("test");
        var handler = new TestCommandHandler(executionLog);

        // Act
        var pipeline = PipelineBuilder.BuildCommandPipeline<TestCommand, string>(sp, command, handler, new NexumOptions());
        var result = await pipeline(CancellationToken.None);

        // Assert — behaviors with order 0 preserve insertion order (NoOrderA before NoOrderB)
        result.Should().Be("handled:test");
        executionLog.Should().ContainInOrder(
            "NoOrderA:Before",
            "NoOrderB:Before",
            "Order1Stable:Before",
            "Order5:Before",
            "Handler",
            "Order5:After",
            "Order1Stable:After",
            "NoOrderB:After",
            "NoOrderA:After"
        );
        executionLog.Count.Should().Be(9);
    }

    // Test types
    private sealed record TestCommand(string Value) : ICommand<string>;

    private sealed class TestCommandHandler(List<string> executionLog) : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            executionLog.Add("Handler");
            return ValueTask.FromResult($"handled:{command.Value}");
        }
    }

    private sealed class TestBehaviorA(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("BehaviorA:Before");
            var result = await next(ct);
            executionLog.Add("BehaviorA:After");
            return result;
        }
    }

    private sealed class TestBehaviorB(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("BehaviorB:Before");
            var result = await next(ct);
            executionLog.Add("BehaviorB:After");
            return result;
        }
    }

    private sealed class TestBehaviorC(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("BehaviorC:Before");
            var result = await next(ct);
            executionLog.Add("BehaviorC:After");
            return result;
        }
    }

    [BehaviorOrder(1)]
    private sealed class TestBehaviorWithOrder1(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("Order1:Before");
            var result = await next(ct);
            executionLog.Add("Order1:After");
            return result;
        }
    }

    [BehaviorOrder(10)]
    private sealed class TestBehaviorWithOrder10(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("Order10:Before");
            var result = await next(ct);
            executionLog.Add("Order10:After");
            return result;
        }
    }

    private sealed class TestBehaviorNoOrderA(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("NoOrderA:Before");
            var result = await next(ct);
            executionLog.Add("NoOrderA:After");
            return result;
        }
    }

    private sealed class TestBehaviorNoOrderB(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("NoOrderB:Before");
            var result = await next(ct);
            executionLog.Add("NoOrderB:After");
            return result;
        }
    }

    [BehaviorOrder(5)]
    private sealed class TestBehaviorWithOrder5(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("Order5:Before");
            var result = await next(ct);
            executionLog.Add("Order5:After");
            return result;
        }
    }

    [BehaviorOrder(1)]
    private sealed class TestBehaviorWithOrder1Stable(List<string> executionLog) : ICommandBehavior<TestCommand, string>
    {
        public async ValueTask<string> HandleAsync(TestCommand command, CommandHandlerDelegate<string> next, CancellationToken ct = default)
        {
            executionLog.Add("Order1Stable:Before");
            var result = await next(ct);
            executionLog.Add("Order1Stable:After");
            return result;
        }
    }
}
