#pragma warning disable CS1591

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Batching;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.Benchmarks;

/// <summary>
/// Benchmark: Individual dispatch vs Batched dispatch.
/// Compares sequential and concurrent query dispatch with and without batching.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public sealed class BatchingBenchmarks
{
    private IQueryDispatcher _noBatchingDispatcher = null!;
    private IQueryDispatcher _batchingDispatcher = null!;
    private ServiceProvider _noBatchingSp = null!;
    private ServiceProvider _batchingSp = null!;

    private const int QueryCount = 100;
    private readonly GetUserByIdQuery[] _queries = Enumerable.Range(1, QueryCount)
        .Select(id => new GetUserByIdQuery(id))
        .ToArray();

    [GlobalSetup]
    public void Setup()
    {
        // Setup WITHOUT batching (baseline)
        var noBatchingServices = new ServiceCollection();
        noBatchingServices.AddLogging();
        noBatchingServices.AddNexum(assemblies: [typeof(GetUserByIdQueryHandler).Assembly]);

        _noBatchingSp = noBatchingServices.BuildServiceProvider();
        _noBatchingDispatcher = _noBatchingSp.GetRequiredService<IQueryDispatcher>();

        // Setup WITH batching
        var batchingServices = new ServiceCollection();
        batchingServices.AddLogging();
        batchingServices.AddNexum(assemblies: [typeof(GetUserByIdQueryHandler).Assembly]);
        batchingServices.AddNexumBatching(
            configure: opts =>
            {
                opts.BatchWindow = TimeSpan.FromMilliseconds(10);
                opts.MaxBatchSize = 100;
            },
            assemblies: [typeof(GetUserByIdBatchHandler).Assembly]);

        _batchingSp = batchingServices.BuildServiceProvider();
        _batchingDispatcher = _batchingSp.GetRequiredService<IQueryDispatcher>();

        // Warm up both dispatchers
        _noBatchingDispatcher.DispatchAsync(_queries[0], CancellationToken.None).AsTask().GetAwaiter().GetResult();
        _batchingDispatcher.DispatchAsync(_queries[0], CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Baseline: 100 sequential DispatchAsync calls without batching.
    /// Each query hits the handler individually.
    /// </summary>
    [Benchmark(Baseline = true)]
    public async Task IndividualDispatches_100QueriesAsync()
    {
        foreach (var query in _queries)
        {
            await _noBatchingDispatcher.DispatchAsync(query, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 100 sequential DispatchAsync calls WITH batching enabled.
    /// Queries are collected over the batch window and executed together.
    /// </summary>
    [Benchmark]
    public async Task BatchedDispatches_100QueriesAsync()
    {
        foreach (var query in _queries)
        {
            await _batchingDispatcher.DispatchAsync(query, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 100 concurrent Task.WhenAll dispatches with batching.
    /// Demonstrates natural batching when queries arrive simultaneously.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentDispatches_100QueriesAsync()
    {
        var tasks = _queries
            .Select(q => _batchingDispatcher.DispatchAsync(q, CancellationToken.None).AsTask())
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _noBatchingSp.Dispose();
        _batchingSp.Dispose();
    }
}

// Query and result types for benchmarking
public sealed record GetUserByIdQuery(int UserId) : IQuery<UserDto>;

public sealed record UserDto(int Id, string Name, string Email);

// Non-batched handler (baseline)
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    // Simulates individual database lookups
    public ValueTask<UserDto> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default)
    {
        // Simulate some work (in real scenarios this would be a DB call)
        var result = new UserDto(query.UserId, $"User{query.UserId}", $"user{query.UserId}@example.com");
        return ValueTask.FromResult(result);
    }
}

// Batched handler (optimized)
public sealed class GetUserByIdBatchHandler : IBatchQueryHandler<GetUserByIdQuery, int, UserDto>
{
    public int GetKey(GetUserByIdQuery query)
    {
        return query.UserId;
    }

    public ValueTask<IReadOnlyDictionary<int, UserDto>> HandleAsync(
        IReadOnlyList<GetUserByIdQuery> queries,
        CancellationToken ct = default)
    {
        // Simulates a single batched database query (e.g., SELECT * FROM Users WHERE Id IN (...))
        var results = queries.ToDictionary(
            q => q.UserId,
            q => new UserDto(q.UserId, $"User{q.UserId}", $"user{q.UserId}@example.com"));

        return ValueTask.FromResult<IReadOnlyDictionary<int, UserDto>>(results);
    }
}
