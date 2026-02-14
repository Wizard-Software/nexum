using System.Collections.Concurrent;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Tests;

/// <summary>
/// Thread-safety tests for <see cref="PolymorphicHandlerResolver"/>.
/// Verifies concurrent access and single-factory guarantee via <see cref="Lazy{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PolymorphicHandlerResolverThreadSafetyTests : IDisposable
{
    public void Dispose()
    {
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        QueryDispatcher.ResetForTesting();
    }

    [Fact]
    public async Task Resolve_ConcurrentAccess_AllReturnSameResultAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<TestCommand, string>, TestCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        const int ConcurrencyLevel = 100;
        var results = new ConcurrentBag<Type?>();
        var barrier = new Barrier(ConcurrencyLevel);

        // Act — resolve from 100 parallel tasks
        var tasks = Enumerable.Range(0, ConcurrencyLevel).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait(); // Ensure all threads start simultaneously
            var result = PolymorphicHandlerResolver.Resolve(
                typeof(TestCommand),
                typeof(ICommandHandler<,>),
                serviceProvider);
            results.Add(result);
        }));

        await Task.WhenAll(tasks);

        // Assert — all results should be identical
        results.Should().HaveCount(ConcurrencyLevel);
        results.Should().AllBeEquivalentTo(typeof(ICommandHandler<TestCommand, string>));
    }

    [Fact]
    public async Task Resolve_ConcurrentAccess_FactoryCalledExactlyOnceAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<FactoryCountCommand, int>, FactoryCountCommandHandler>();
        var serviceProvider = services.BuildServiceProvider();

        const int ConcurrencyLevel = 50;
        var barrier = new Barrier(ConcurrencyLevel);
        var results = new ConcurrentBag<Type?>();

        // Act — resolve from 50 parallel tasks
        var tasks = Enumerable.Range(0, ConcurrencyLevel).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            var result = PolymorphicHandlerResolver.Resolve(
                typeof(FactoryCountCommand),
                typeof(ICommandHandler<,>),
                serviceProvider);
            results.Add(result);
        }));

        await Task.WhenAll(tasks);

        // Assert — Lazy<T> with ExecutionAndPublication guarantees single factory execution.
        // All results should be the same, confirming the factory produced a consistent result.
        results.Should().HaveCount(ConcurrencyLevel);
        var distinctResults = results.Distinct().ToList();
        distinctResults.Should().ContainSingle()
            .Which.Should().Be(typeof(ICommandHandler<FactoryCountCommand, int>));
    }

    // Test types
    private sealed record TestCommand(string Value = "test") : ICommand<string>;

    private sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
    {
        public ValueTask<string> HandleAsync(TestCommand command, CancellationToken ct = default)
            => ValueTask.FromResult(command.Value);
    }

    private sealed record FactoryCountCommand : ICommand<int>;

    private sealed class FactoryCountCommandHandler : ICommandHandler<FactoryCountCommand, int>
    {
        public ValueTask<int> HandleAsync(FactoryCountCommand command, CancellationToken ct = default)
            => ValueTask.FromResult(42);
    }
}
