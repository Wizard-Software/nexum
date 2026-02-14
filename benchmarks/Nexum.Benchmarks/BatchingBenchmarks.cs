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
/// Simulates 1ms DB latency per handler call to demonstrate batching's value:
/// batched queries execute with a single I/O round-trip instead of N separate ones.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class BatchingBenchmarks
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
    public async Task SetupAsync()
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
                opts.BatchWindow = TimeSpan.FromMilliseconds(1);
                opts.MaxBatchSize = 100;
            },
            assemblies: [typeof(GetUserByIdBatchHandler).Assembly]);

        _batchingSp = batchingServices.BuildServiceProvider();
        _batchingDispatcher = _batchingSp.GetRequiredService<IQueryDispatcher>();

        // Warm up both dispatchers
        await _noBatchingDispatcher.DispatchAsync(_queries[0], CancellationToken.None).ConfigureAwait(false);
        await _batchingDispatcher.DispatchAsync(_queries[0], CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Baseline: 100 sequential queries without batching.
    /// Each query pays 1ms simulated DB latency individually (~100ms total).
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
    /// 100 concurrent queries WITHOUT batching.
    /// Queries run in parallel but each still pays 1ms DB latency.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentDispatches_NoBatching_100QueriesAsync()
    {
        var tasks = _queries
            .Select(q => _noBatchingDispatcher.DispatchAsync(q, CancellationToken.None).AsTask())
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// 100 concurrent queries WITH batching.
    /// All queries are collected into a single batch, paying only 1ms DB latency once.
    /// </summary>
    [Benchmark]
    public async Task ConcurrentDispatches_Batched_100QueriesAsync()
    {
        var tasks = _queries
            .Select(q => _batchingDispatcher.DispatchAsync(q, CancellationToken.None).AsTask())
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _noBatchingSp.DisposeAsync().ConfigureAwait(false);
        await _batchingSp.DisposeAsync().ConfigureAwait(false);
    }
}

// Query and result types for benchmarking
public sealed record GetUserByIdQuery(int UserId) : IQuery<UserDto>;

public sealed record UserDto(int Id, string Name, string Email);

// Non-batched handler — 1ms delay per query simulates individual DB call
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto>
{
    public async ValueTask<UserDto> HandleAsync(GetUserByIdQuery query, CancellationToken ct = default)
    {
        await Task.Delay(1, ct).ConfigureAwait(false);
        return new UserDto(query.UserId, $"User{query.UserId}", $"user{query.UserId}@example.com");
    }
}

// Batched handler — 1ms delay for entire batch simulates single DB call with IN clause
public sealed class GetUserByIdBatchHandler : IBatchQueryHandler<GetUserByIdQuery, int, UserDto>
{
    public int GetKey(GetUserByIdQuery query)
    {
        return query.UserId;
    }

    public async ValueTask<IReadOnlyDictionary<int, UserDto>> HandleAsync(
        IReadOnlyList<GetUserByIdQuery> queries,
        CancellationToken ct = default)
    {
        await Task.Delay(1, ct).ConfigureAwait(false);
        var results = queries.ToDictionary(
            q => q.UserId,
            q => new UserDto(q.UserId, $"User{q.UserId}", $"user{q.UserId}@example.com"));

        return results;
    }
}
