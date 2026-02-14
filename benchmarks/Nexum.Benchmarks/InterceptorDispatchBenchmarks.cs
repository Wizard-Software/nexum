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
public class InterceptorDispatchBenchmarks  // unsealed — BDN requirement
{
    private ICommandDispatcher _runtimeDispatcher = null!;
    private ICommandDispatcher _sgDispatcher = null!;
    private IInterceptableDispatcher _tier3Interceptable = null!;
    private ServiceProvider _runtimeSp = null!;
    private ServiceProvider _sgSp = null!;
    private ServiceProvider _tier3Sp = null!;
    private readonly BenchCommand _command = new("bench");
    private Func<BenchCommand, IServiceProvider, CancellationToken, ValueTask<Guid>> _compiledPipeline = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Runtime path (Tier 1)
        var runtimeServices = new ServiceCollection();
        var runtimeOptions = new NexumOptions { MaxDispatchDepth = int.MaxValue };
        runtimeServices.AddSingleton(runtimeOptions);
        runtimeServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        runtimeServices.AddSingleton<ExceptionHandlerResolver>();
        runtimeServices.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        runtimeServices.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
        _runtimeSp = runtimeServices.BuildServiceProvider();
        _runtimeDispatcher = _runtimeSp.GetRequiredService<ICommandDispatcher>();

        // Warm up runtime path (populate caches)
        _runtimeDispatcher.DispatchAsync(_command, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        // Reset caches before SG setup
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // Source Generator Tier 2 path
        var sgServices = new ServiceCollection();
        var sgOptions = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistry), MaxDispatchDepth = int.MaxValue };
        sgServices.AddSingleton(sgOptions);
        sgServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        sgServices.AddSingleton<ExceptionHandlerResolver>();
        sgServices.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        sgServices.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
        sgServices.AddScoped<BenchCommandHandler>();
        _sgSp = sgServices.BuildServiceProvider();
        _sgDispatcher = _sgSp.GetRequiredService<ICommandDispatcher>();

        // Warm up SG path
        _sgDispatcher.DispatchAsync(_command, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        // Reset caches before Tier 3 setup
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // Source Generator Tier 3 path (Intercepted)
        // Use same setup as Tier 2 — the difference is in how we call it
        var tier3Services = new ServiceCollection();
        var tier3Options = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistry), MaxDispatchDepth = int.MaxValue };
        tier3Services.AddSingleton(tier3Options);
        tier3Services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        tier3Services.AddSingleton<ExceptionHandlerResolver>();
        tier3Services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        tier3Services.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
        tier3Services.AddScoped<BenchCommandHandler>();
        _tier3Sp = tier3Services.BuildServiceProvider();

        var tier3Dispatcher = _tier3Sp.GetRequiredService<ICommandDispatcher>();
        _tier3Interceptable = (IInterceptableDispatcher)tier3Dispatcher;

        // Cache compiled pipeline delegate (simulates what SG would generate)
        _compiledPipeline = static (cmd, sp, ct) =>
            sp.GetRequiredService<BenchCommandHandler>().HandleAsync(cmd, ct);

        // Warm up Tier 3 path
        _tier3Interceptable.DispatchInterceptedCommandAsync(_command, _compiledPipeline, CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> DispatchCommand_Runtime()
        => _runtimeDispatcher.DispatchAsync(_command, CancellationToken.None);

    [Benchmark]
    public ValueTask<Guid> DispatchCommand_Tier2_SG()
        => _sgDispatcher.DispatchAsync(_command, CancellationToken.None);

    [Benchmark]
    public ValueTask<Guid> DispatchCommand_Tier3_Intercepted()
        => _tier3Interceptable.DispatchInterceptedCommandAsync(
            _command,
            _compiledPipeline,
            CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtimeSp.Dispose();
        _sgSp.Dispose();
        _tier3Sp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
