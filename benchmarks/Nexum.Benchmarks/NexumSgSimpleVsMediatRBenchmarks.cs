#pragma warning disable CS1591

using System.Threading.Channels;
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
public class NexumSgSimpleVsMediatRBenchmarks
{
    private ICommandDispatcher _nexumSgDispatcher = null!;
    private INotificationPublisher _nexumNotifPublisher = null!;
    private MediatR.IMediator _mediator = null!;
    private MediatR.IMediator _mediatorForNotifs = null!;
    private ServiceProvider _nexumSgSp = null!;
    private ServiceProvider _nexumNotifSp = null!;
    private ServiceProvider _mediatRSp = null!;
    private ServiceProvider _mediatRNotifSp = null!;

    private readonly BenchCommand _nexumCommand = new("bench");
    private readonly BenchNotification _nexumNotification = new("bench");
    private readonly MediatRBenchCommand _mediatRCommand = new("bench");
    private readonly MediatRBenchNotification _mediatRNotification = new("bench");

    [GlobalSetup]
    public void Setup()
    {
        // === Nexum SG: Simple command (Tier 2, no behaviors) ===
        {
            var services = new ServiceCollection();
            var options = new NexumOptions { PipelineRegistryType = typeof(BenchPipelineRegistry), MaxDispatchDepth = int.MaxValue };
            services.AddSingleton(options);
            services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
            services.AddSingleton<ExceptionHandlerResolver>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
            services.AddScoped<BenchCommandHandler>();
            _nexumSgSp = services.BuildServiceProvider();
            _nexumSgDispatcher = _nexumSgSp.GetRequiredService<ICommandDispatcher>();
            _nexumSgDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        // Reset static caches between Nexum command and notification setups
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // === Nexum: Notifications (Runtime path — Tier 2 doesn't support notifications) ===
        {
            var services = new ServiceCollection();
            var options = new NexumOptions { MaxDispatchDepth = int.MaxValue };
            services.AddSingleton(options);
            services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
            services.AddSingleton<ExceptionHandlerResolver>();
            var channel = Channel.CreateBounded<NotificationEnvelope>(1000);
            services.AddSingleton<INotificationPublisher>(sp =>
                new NotificationPublisher(sp, sp.GetRequiredService<NexumOptions>(), channel.Writer));
            services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler1>();
            services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler2>();
            services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler3>();
            services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler4>();
            services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler5>();
            _nexumNotifSp = services.BuildServiceProvider();
            _nexumNotifPublisher = _nexumNotifSp.GetRequiredService<INotificationPublisher>();
            _nexumNotifPublisher.PublishAsync(_nexumNotification, PublishStrategy.Sequential, CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
        }

        // === MediatR: Simple command (no behaviors) ===
        {
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRBenchCommandHandler>());
            _mediatRSp = services.BuildServiceProvider();
            _mediator = _mediatRSp.GetRequiredService<MediatR.IMediator>();
            _mediator.Send(_mediatRCommand, CancellationToken.None).GetAwaiter().GetResult();
        }

        // === MediatR: Notifications (5 handlers) ===
        {
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRBenchNotificationHandler1>());
            _mediatRNotifSp = services.BuildServiceProvider();
            _mediatorForNotifs = _mediatRNotifSp.GetRequiredService<MediatR.IMediator>();
            _mediatorForNotifs.Publish(_mediatRNotification, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> NexumSG_SimpleCommand()
        => _nexumSgDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public Task<Guid> MediatR_SimpleCommand()
        => _mediator.Send(_mediatRCommand, CancellationToken.None);

    [Benchmark]
    public ValueTask NexumSG_5NotificationHandlers()
        => _nexumNotifPublisher.PublishAsync(_nexumNotification, PublishStrategy.Sequential, CancellationToken.None);

    [Benchmark]
    public Task MediatR_5NotificationHandlers()
        => _mediatorForNotifs.Publish(_mediatRNotification, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _nexumSgSp.Dispose();
        _nexumNotifSp.Dispose();
        _mediatRSp.Dispose();
        _mediatRNotifSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
