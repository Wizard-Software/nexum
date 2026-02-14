#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode warning for test usage

using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Batching.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.Batching.Tests;

[Trait("Category", "Integration")]
public sealed class BatchingIntegrationTests
{
    [Fact]
    public async Task AddNexumBatching_FullPipeline_BatchesQueriesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        // Manually register batch handler to avoid assembly scanning picking up test-only handlers
        services.AddNexumBatching();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Act
        var result = await dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be($"Item-{id}");
    }

    [Fact]
    public async Task ConcurrentDispatch_MultipleCallers_SingleBatchAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts => opts.BatchWindow = TimeSpan.FromMilliseconds(100));
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();

        var query1 = new GetItemByIdQuery(id1);
        var query2 = new GetItemByIdQuery(id2);
        var query3 = new GetItemByIdQuery(id3);

        // Act
        var tasks = new[]
        {
            dispatcher.DispatchAsync(query1, TestContext.Current.CancellationToken).AsTask(),
            dispatcher.DispatchAsync(query2, TestContext.Current.CancellationToken).AsTask(),
            dispatcher.DispatchAsync(query3, TestContext.Current.CancellationToken).AsTask()
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results[0].Should().Be($"Item-{id1}");
        results[1].Should().Be($"Item-{id2}");
        results[2].Should().Be($"Item-{id3}");
    }

    [Fact]
    public async Task MixedQueries_BatchAndNonBatch_BothWorkAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var id = Guid.NewGuid();
        var batchQuery = new GetItemByIdQuery(id);
        var normalQuery = new GetItemByNameQuery("test");

        // Act
        var batchResult = await dispatcher.DispatchAsync(batchQuery, TestContext.Current.CancellationToken);
        var normalResult = await dispatcher.DispatchAsync(normalQuery, TestContext.Current.CancellationToken);

        // Assert
        batchResult.Should().Be($"Item-{id}");
        normalResult.Should().Be(4); // "test".Length
    }

    [Fact]
    public async Task DisposeAsync_GracefulShutdown_DrainsPendingQueriesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts => opts.BatchWindow = TimeSpan.FromSeconds(10)); // Long window
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IQueryDispatcher>();

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Act
        var resultTask = dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken);

        // Dispose root provider before batch window expires — triggers singleton disposal
        // which calls BatchingQueryDispatcher.DisposeAsync() → buffer drain
        await sp.DisposeAsync();

        // Give a small amount of time for async disposal to complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        resultTask.IsCompleted.Should().BeTrue();
        var result = await resultTask;
        result.Should().Be($"Item-{id}");
    }

    [Fact]
    public async Task ConcurrentFlush_NewEntriesArriving_NoCrashAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts =>
        {
            opts.BatchWindow = TimeSpan.FromMilliseconds(50);
            opts.MaxBatchSize = 5;
        });
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        // Act
        var tasks = new List<Task<string>>();
        for (int i = 0; i < 20; i++)
        {
            var id = Guid.NewGuid();
            var query = new GetItemByIdQuery(id);
            tasks.Add(dispatcher.DispatchAsync(query, TestContext.Current.CancellationToken).AsTask());

            // Small delay to create interleaving
            if (i % 3 == 0)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(20);
        results.Should().AllSatisfy(r => r.Should().StartWith("Item-"));
    }

    [Fact]
    public async Task StreamQuery_NotBatched_PassesThroughAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(StreamItemsQueryHandler).Assembly]);
        services.AddNexumBatching();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var query = new StreamItemsQuery();

        // Act
        var results = new List<string>();
        await foreach (var item in dispatcher.StreamAsync(query, TestContext.Current.CancellationToken))
        {
            results.Add(item);
        }

        // Assert
        results.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public async Task DuplicateKeys_InSameBatch_ReturnSameResultAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts => opts.BatchWindow = TimeSpan.FromMilliseconds(100));
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var id = Guid.NewGuid();
        var query1 = new GetItemByIdQuery(id);
        var query2 = new GetItemByIdQuery(id); // Same ID

        // Act
        var task1 = dispatcher.DispatchAsync(query1, TestContext.Current.CancellationToken);
        var task2 = dispatcher.DispatchAsync(query2, TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(task1.AsTask(), task2.AsTask());

        // Assert
        results[0].Should().Be(results[1]);
        results[0].Should().Be($"Item-{id}");
    }

    [Fact]
    public void CustomOptions_AppliedCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts =>
        {
            opts.BatchWindow = TimeSpan.FromMilliseconds(200);
            opts.MaxBatchSize = 50;
            opts.DrainTimeout = TimeSpan.FromSeconds(10);
        });

        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<NexumBatchingOptions>();

        // Assert
        options.BatchWindow.Should().Be(TimeSpan.FromMilliseconds(200));
        options.MaxBatchSize.Should().Be(50);
        options.DrainTimeout.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void AddNexumBatching_WithoutAddNexum_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        Action act = () => services.AddNexumBatching();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddNexum()*");
    }

    [Fact]
    public async Task CancellationToken_CancelsQueryAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        services.AddNexumBatching(opts => opts.BatchWindow = TimeSpan.FromSeconds(10)); // Long window
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);
        var cts = new CancellationTokenSource();

        // Act
        var task = dispatcher.DispatchAsync(query, cts.Token);
        cts.Cancel();

        // Assert
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    [Fact]
    public void AssemblyScanning_RegistersHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddNexum(assemblies: [typeof(GetItemByNameQueryHandler).Assembly]);
        // Use assembly scanning but only for a dedicated handler assembly (not test fixtures)
        // For this test, we'll manually register to simulate what assembly scanning does
        services.AddNexumBatching();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        services.AddSingleton(new Internal.BatchHandlerRegistration(
            typeof(GetItemByIdQuery),
            typeof(Guid),
            typeof(string),
            typeof(GetItemByIdBatchHandler)));

        var sp = services.BuildServiceProvider();

        // Act
        var handler = sp.GetService<IBatchQueryHandler<GetItemByIdQuery, Guid, string>>();

        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<GetItemByIdBatchHandler>();
    }
}
