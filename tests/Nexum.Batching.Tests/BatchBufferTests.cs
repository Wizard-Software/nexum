using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nexum.Batching.Internal;
using Nexum.Batching.Tests.Fixtures;

namespace Nexum.Batching.Tests;

[Trait("Category", "Unit")]
public sealed class BatchBufferTests
{
    [Fact]
    public async Task FlushAsync_AfterBatchWindow_FlushesEntriesAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(50),
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Act
        var resultTask = buffer.EnqueueAsync(query, TestContext.Current.CancellationToken);

        // Wait for batch window + small buffer
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        resultTask.IsCompleted.Should().BeTrue();
        var result = await resultTask;
        result.Should().Be($"Item-{id}");

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task FlushAsync_AtMaxBatchSize_FlushesImmediatelyAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromSeconds(10), // Large window
            MaxBatchSize = 2 // Small batch size for quick test
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var query1 = new GetItemByIdQuery(id1);
        var query2 = new GetItemByIdQuery(id2);

        // Act
        var task1 = buffer.EnqueueAsync(query1, TestContext.Current.CancellationToken);
        var task2 = buffer.EnqueueAsync(query2, TestContext.Current.CancellationToken);

        // Should flush immediately without waiting for batch window
        await Task.WhenAll(task1.AsTask(), task2.AsTask());

        // Assert
        task1.IsCompleted.Should().BeTrue();
        task2.IsCompleted.Should().BeTrue();
        var result1 = await task1;
        var result2 = await task2;
        result1.Should().Be($"Item-{id1}");
        result2.Should().Be($"Item-{id2}");

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAsync_DuplicateKey_ReturnsSameResultAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(50),
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id = Guid.NewGuid();
        var query1 = new GetItemByIdQuery(id);
        var query2 = new GetItemByIdQuery(id); // Same ID

        // Act
        var task1 = buffer.EnqueueAsync(query1, TestContext.Current.CancellationToken);
        var task2 = buffer.EnqueueAsync(query2, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        var result1 = await task1;
        var result2 = await task2;
        result1.Should().Be(result2);
        result1.Should().Be($"Item-{id}");

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task FlushAsync_MissingKeyInResult_ThrowsKeyNotFoundExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, IncompleteResultBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(50),
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var query1 = new GetItemByIdQuery(id1);
        var query2 = new GetItemByIdQuery(id2);

        // Act
        var task1 = buffer.EnqueueAsync(query1, TestContext.Current.CancellationToken);
        var task2 = buffer.EnqueueAsync(query2, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert — IncompleteResultBatchHandler returns only the first query's result.
        // ConcurrentDictionary iteration order is not guaranteed, so one task succeeds
        // and the other throws KeyNotFoundException.
        var tasks = new[] { task1.AsTask(), task2.AsTask() };
        int succeeded = 0;
        int failed = 0;

        foreach (Task<string> task in tasks)
        {
            try
            {
                await task;
                succeeded++;
            }
            catch (KeyNotFoundException)
            {
                failed++;
            }
        }

        succeeded.Should().Be(1, "one query should get a result");
        failed.Should().Be(1, "one query should throw KeyNotFoundException");

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task FlushAsync_HandlerThrows_PropagatesExceptionToAllCallersAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, ThrowingBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(50),
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var query1 = new GetItemByIdQuery(id1);
        var query2 = new GetItemByIdQuery(id2);

        // Act
        var task1 = buffer.EnqueueAsync(query1, TestContext.Current.CancellationToken);
        var task2 = buffer.EnqueueAsync(query2, TestContext.Current.CancellationToken);

        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);

        // Assert
        Func<Task> act1 = async () => await task1;
        Func<Task> act2 = async () => await task2;

        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Batch handler failed");
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Batch handler failed");

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAsync_CancellationToken_CancelsEntryAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromMilliseconds(100), // Longer window
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);
        var cts = new CancellationTokenSource();

        // Act
        var task = buffer.EnqueueAsync(query, cts.Token);
        cts.Cancel();

        // Assert
        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<TaskCanceledException>();

        await buffer.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_PendingEntries_DrainsFlushedAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions
        {
            BatchWindow = TimeSpan.FromSeconds(10), // Very long window
            MaxBatchSize = 100
        };

        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        var id = Guid.NewGuid();
        var query = new GetItemByIdQuery(id);

        // Act
        var task = buffer.EnqueueAsync(query, TestContext.Current.CancellationToken);

        // Dispose before batch window expires - this triggers flush
        await buffer.DisposeAsync();

        // Give a small amount of time for async disposal to complete
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert
        task.IsCompleted.Should().BeTrue();
        var result = await task;
        result.Should().Be($"Item-{id}");
    }

    [Fact]
    public async Task EnqueueAsync_AfterDispose_ThrowsObjectDisposedExceptionAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IBatchQueryHandler<GetItemByIdQuery, Guid, string>, GetItemByIdBatchHandler>();
        var sp = services.BuildServiceProvider();

        var options = new NexumBatchingOptions();
        var buffer = new BatchBuffer<GetItemByIdQuery, Guid, string>(
            options,
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger.Instance);

        await buffer.DisposeAsync();

        var query = new GetItemByIdQuery(Guid.NewGuid());

        // Act
        Action act = () => buffer.EnqueueAsync(query, TestContext.Current.CancellationToken);

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }
}
