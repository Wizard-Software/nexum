#pragma warning disable CS1591

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Nexum.Abstractions;
using Nexum.Benchmarks.Setup;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class QueryDispatchBenchmarks
{
    private IQueryDispatcher _runtimeDispatcher = null!;
    private IQueryDispatcher _sgDispatcher = null!;
    private ServiceProvider _runtimeSp = null!;
    private ServiceProvider _sgSp = null!;
    private readonly BenchQuery _query = new(42);
    private readonly BenchStreamQuery _streamQuery = new(10_000);

    [GlobalSetup]
    public void Setup()
    {
        // Runtime path
        var runtimeServices = new ServiceCollection();
        var runtimeOptions = new NexumOptions { MaxDispatchDepth = int.MaxValue };
        runtimeServices.AddSingleton(runtimeOptions);
        runtimeServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        runtimeServices.AddSingleton<ExceptionHandlerResolver>();
        runtimeServices.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        runtimeServices.AddScoped<IQueryHandler<BenchQuery, string>, BenchQueryHandler>();
        runtimeServices.AddScoped<IStreamQueryHandler<BenchStreamQuery, int>, BenchStreamQueryHandler>();
        _runtimeSp = runtimeServices.BuildServiceProvider();
        _runtimeDispatcher = _runtimeSp.GetRequiredService<IQueryDispatcher>();

        // Warm up runtime path
        _runtimeDispatcher.DispatchAsync(_query, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        // Reset caches before SG setup
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // Source Generator (Tier 2) path
        var sgServices = new ServiceCollection();
        var sgOptions = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistry), MaxDispatchDepth = int.MaxValue };
        sgServices.AddSingleton(sgOptions);
        sgServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        sgServices.AddSingleton<ExceptionHandlerResolver>();
        sgServices.AddSingleton<IQueryDispatcher, QueryDispatcher>();
        sgServices.AddScoped<IQueryHandler<BenchQuery, string>, BenchQueryHandler>();
        sgServices.AddScoped<BenchQueryHandler>();
        _sgSp = sgServices.BuildServiceProvider();
        _sgDispatcher = _sgSp.GetRequiredService<IQueryDispatcher>();

        // Warm up SG path
        _sgDispatcher.DispatchAsync(_query, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<string> DispatchQuery_NoBehaviors_Runtime()
        => _runtimeDispatcher.DispatchAsync(_query, CancellationToken.None);

    [Benchmark]
    public ValueTask<string> DispatchQuery_NoBehaviors_SourceGen()
        => _sgDispatcher.DispatchAsync(_query, CancellationToken.None);

    [Benchmark]
    public async Task<int> StreamQuery_10KItems_RuntimeAsync()
    {
        var count = 0;
        await foreach (var item in _runtimeDispatcher.StreamAsync(_streamQuery, CancellationToken.None).ConfigureAwait(false))
        {
            count = item;
        }
        return count;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtimeSp.Dispose();
        _sgSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        QueryDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
