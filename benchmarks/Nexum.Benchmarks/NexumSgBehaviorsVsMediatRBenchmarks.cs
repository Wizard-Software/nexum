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
public class NexumSgBehaviorsVsMediatRBenchmarks
{
    private ICommandDispatcher _nexumSgDispatcher = null!;
    private MediatR.IMediator _mediatorWithBehaviors = null!;
    private ServiceProvider _nexumSgSp = null!;
    private ServiceProvider _mediatRSp = null!;

    private readonly BenchCommand _nexumCommand = new("bench");
    private readonly MediatRBenchCommand _mediatRCommand = new("bench");

    [GlobalSetup]
    public void Setup()
    {
        // === Nexum SG: 3 compiled behaviors (Tier 2) ===
        {
            var services = new ServiceCollection();
            var options = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistryWithBehaviors), MaxDispatchDepth = int.MaxValue };
            services.AddSingleton(options);
            services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
            services.AddSingleton<ExceptionHandlerResolver>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
            services.AddScoped<BenchCommandHandler>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior1>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior2>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior3>();
            services.AddScoped<BenchCommandBehavior1>();
            services.AddScoped<BenchCommandBehavior2>();
            services.AddScoped<BenchCommandBehavior3>();
            _nexumSgSp = services.BuildServiceProvider();
            _nexumSgDispatcher = _nexumSgSp.GetRequiredService<ICommandDispatcher>();
            _nexumSgDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        // === MediatR: 3 behaviors ===
        {
            var services = new ServiceCollection();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining<MediatRBenchCommandHandler>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior1>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior2>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior3>();
            });
            _mediatRSp = services.BuildServiceProvider();
            _mediatorWithBehaviors = _mediatRSp.GetRequiredService<MediatR.IMediator>();
            _mediatorWithBehaviors.Send(_mediatRCommand, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> NexumSG_3Behaviors()
        => _nexumSgDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public Task<Guid> MediatR_3Behaviors()
        => _mediatorWithBehaviors.Send(_mediatRCommand, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _nexumSgSp.Dispose();
        _mediatRSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
