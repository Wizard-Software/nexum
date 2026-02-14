using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Batching.Tests.Fixtures;

namespace Nexum.Batching.Tests;

[Trait("Category", "Unit")]
public sealed class BatchingQueryDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_NoBatchHandler_PassesThroughToInnerAsync()
    {
        // Arrange
        var innerDispatcher = Substitute.For<IQueryDispatcher>();
        var query = new GetItemByNameQuery("test");
        const int ExpectedResult = 4;

        innerDispatcher.DispatchAsync(query, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ExpectedResult));

        var services = new ServiceCollection();
        services.AddSingleton(new NexumBatchingOptions());
        // No batch handler registered for GetItemByNameQuery
        var sp = services.BuildServiceProvider();

        var dispatcher = new BatchingQueryDispatcher(innerDispatcher, sp.GetRequiredService<NexumBatchingOptions>(), sp);

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(ExpectedResult);
        await innerDispatcher.Received(1).DispatchAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_WithBatchHandler_BatchesQueryAsync()
    {
        // Arrange
        var innerDispatcher = Substitute.For<IQueryDispatcher>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(50)
        });
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();

        var dispatcher = new BatchingQueryDispatcher(innerDispatcher, sp.GetRequiredService<NexumBatchingOptions>(), sp);

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Act
        var resultTask = dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken); // Wait for batch flush

        // Assert
        var result = await resultTask;
        result.Should().Be($"Item-{id}");

        // Inner dispatcher should NOT be called
        await innerDispatcher.DidNotReceive().DispatchAsync(Arg.Any<IQuery<string>>(), Arg.Any<CancellationToken>());

        await dispatcher.DisposeAsync();
    }

    [Fact]
    public async Task StreamAsync_AlwaysPassesThroughAsync()
    {
        // Arrange
        var innerDispatcher = Substitute.For<IQueryDispatcher>();
        var query = new StreamItemsQuery();
        var expectedResults = new[] { "a", "b" };

        async IAsyncEnumerable<string> GetExpectedStreamAsync()
        {
            foreach (var item in expectedResults)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }

        innerDispatcher.StreamAsync(query, Arg.Any<CancellationToken>())
            .Returns(GetExpectedStreamAsync());

        var services = new ServiceCollection();
        services.AddSingleton(new NexumBatchingOptions());
        var sp = services.BuildServiceProvider();

        var dispatcher = new BatchingQueryDispatcher(innerDispatcher, sp.GetRequiredService<NexumBatchingOptions>(), sp);

        // Act
        var results = new List<string>();
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert
        results.Should().BeEquivalentTo(expectedResults);
        innerDispatcher.Received(1).StreamAsync(query, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_NullQuery_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var innerDispatcher = Substitute.For<IQueryDispatcher>();
        var services = new ServiceCollection();
        services.AddSingleton(new NexumBatchingOptions());
        var sp = services.BuildServiceProvider();

        var dispatcher = new BatchingQueryDispatcher(innerDispatcher, sp.GetRequiredService<NexumBatchingOptions>(), sp);

        // Act
        Func<Task> act = async () => await dispatcher.DispatchAsync<string>(null!, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task DisposeAsync_WithActiveBuffers_DisposesBuffersAsync()
    {
        // Arrange
        var innerDispatcher = Substitute.For<IQueryDispatcher>();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromSeconds(10) // Long window
        });
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();

        var dispatcher = new BatchingQueryDispatcher(innerDispatcher, sp.GetRequiredService<NexumBatchingOptions>(), sp);

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Enqueue a query to create a buffer
        var task = dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Act
        await dispatcher.DisposeAsync();

        // Assert
        // Buffer should have flushed during dispose
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().Be($"Item-{id}");
    }
}
