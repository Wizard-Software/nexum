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
public class CommandPipelineBenchmarks
{
    private ICommandDispatcher _runtimeDispatcher = null!;
    private ICommandDispatcher _sgDispatcher = null!;
    private ServiceProvider _runtimeSp = null!;
    private ServiceProvider _sgSp = null!;
    private readonly BenchCommand _command = new("bench");

    [GlobalSetup]
    public void Setup()
    {
        // Runtime path with 3 behaviors
        var runtimeServices = new ServiceCollection();
        var runtimeOptions = new NexumOptions { MaxDispatchDepth = int.MaxValue };
        runtimeServices.AddSingleton(runtimeOptions);
        runtimeServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        runtimeServices.AddSingleton<ExceptionHandlerResolver>();
        runtimeServices.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        runtimeServices.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
        runtimeServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior1>();
        runtimeServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior2>();
        runtimeServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior3>();
        _runtimeSp = runtimeServices.BuildServiceProvider();
        _runtimeDispatcher = _runtimeSp.GetRequiredService<ICommandDispatcher>();

        // Warm up runtime path
        _runtimeDispatcher.DispatchAsync(_command, CancellationToken.None).AsTask().GetAwaiter().GetResult();

        // Reset caches before SG setup
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // Source Generator (Tier 2) path with 3 compiled behaviors
        var sgServices = new ServiceCollection();
        var sgOptions = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistryWithBehaviors), MaxDispatchDepth = int.MaxValue };
        sgServices.AddSingleton(sgOptions);
        sgServices.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        sgServices.AddSingleton<ExceptionHandlerResolver>();
        sgServices.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        sgServices.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
        sgServices.AddScoped<BenchCommandHandler>();
        sgServices.AddScoped<BenchCommandBehavior1>();
        sgServices.AddScoped<BenchCommandBehavior2>();
        sgServices.AddScoped<BenchCommandBehavior3>();
        sgServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior1>();
        sgServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior2>();
        sgServices.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior3>();
        _sgSp = sgServices.BuildServiceProvider();
        _sgDispatcher = _sgSp.GetRequiredService<ICommandDispatcher>();

        // Warm up SG path
        _sgDispatcher.DispatchAsync(_command, CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> DispatchCommand_3Behaviors_Runtime()
        => _runtimeDispatcher.DispatchAsync(_command, CancellationToken.None);

    [Benchmark]
    public ValueTask<Guid> DispatchCommand_3Behaviors_SourceGen()
        => _sgDispatcher.DispatchAsync(_command, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _runtimeSp.Dispose();
        _sgSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
