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
public class NexumVsMediatRBenchmarks
{
    private ICommandDispatcher _nexumDispatcher = null!;
    private ICommandDispatcher _nexumPipelineDispatcher = null!;
    private INotificationPublisher _nexumNotificationPublisher = null!;
    private MediatR.IMediator _mediator = null!;
    private MediatR.IMediator _mediatorWithBehaviors = null!;
    private MediatR.IMediator _mediatorForNotifications = null!;
    private ServiceProvider _nexumSp = null!;
    private ServiceProvider _nexumPipelineSp = null!;
    private ServiceProvider _nexumNotifSp = null!;
    private ServiceProvider _mediatRSp = null!;
    private ServiceProvider _mediatRPipelineSp = null!;
    private ServiceProvider _mediatRNotifSp = null!;

    private readonly BenchCommand _nexumCommand = new("bench");
    private readonly BenchNotification _nexumNotification = new("bench");
    private readonly MediatRBenchCommand _mediatRCommand = new("bench");
    private readonly MediatRBenchNotification _mediatRNotification = new("bench");

    [GlobalSetup]
    public void Setup()
    {
        // === Nexum: Simple command (no behaviors) ===
        {
            var services = new ServiceCollection();
            var options = new NexumOptions { MaxDispatchDepth = int.MaxValue };
            services.AddSingleton(options);
            services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
            services.AddSingleton<ExceptionHandlerResolver>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
            _nexumSp = services.BuildServiceProvider();
            _nexumDispatcher = _nexumSp.GetRequiredService<ICommandDispatcher>();
            _nexumDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        // Reset static caches
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

        // === Nexum: Command with 3 behaviors ===
        {
            var services = new ServiceCollection();
            var options = new NexumOptions { MaxDispatchDepth = int.MaxValue };
            services.AddSingleton(options);
            services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
            services.AddSingleton<ExceptionHandlerResolver>();
            services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
            services.AddScoped<ICommandHandler<BenchCommand, Guid>, BenchCommandHandler>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior1>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior2>();
            services.AddScoped<ICommandBehavior<BenchCommand, Guid>, BenchCommandBehavior3>();
            _nexumPipelineSp = services.BuildServiceProvider();
            _nexumPipelineDispatcher = _nexumPipelineSp.GetRequiredService<ICommandDispatcher>();
            _nexumPipelineDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        }

        // === Nexum: Notifications (5 handlers, Sequential) ===
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
            _nexumNotificationPublisher = _nexumNotifSp.GetRequiredService<INotificationPublisher>();
            _nexumNotificationPublisher.PublishAsync(_nexumNotification, PublishStrategy.Sequential, CancellationToken.None)
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

        // === MediatR: Command with 3 behaviors ===
        {
            var services = new ServiceCollection();
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining<MediatRBenchCommandHandler>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior1>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior2>();
                cfg.AddBehavior<MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>, MediatRBenchBehavior3>();
            });
            _mediatRPipelineSp = services.BuildServiceProvider();
            _mediatorWithBehaviors = _mediatRPipelineSp.GetRequiredService<MediatR.IMediator>();
            _mediatorWithBehaviors.Send(_mediatRCommand, CancellationToken.None).GetAwaiter().GetResult();
        }

        // === MediatR: Notifications (5 handlers) ===
        {
            var services = new ServiceCollection();
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRBenchNotificationHandler1>());
            _mediatRNotifSp = services.BuildServiceProvider();
            _mediatorForNotifications = _mediatRNotifSp.GetRequiredService<MediatR.IMediator>();
            _mediatorForNotifications.Publish(_mediatRNotification, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> Nexum_SimpleCommand()
        => _nexumDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public Task<Guid> MediatR_SimpleCommand()
        => _mediator.Send(_mediatRCommand, CancellationToken.None);

    [Benchmark]
    public ValueTask<Guid> Nexum_3Behaviors()
        => _nexumPipelineDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public Task<Guid> MediatR_3Behaviors()
        => _mediatorWithBehaviors.Send(_mediatRCommand, CancellationToken.None);

    [Benchmark]
    public ValueTask Nexum_5NotificationHandlers()
        => _nexumNotificationPublisher.PublishAsync(_nexumNotification, PublishStrategy.Sequential, CancellationToken.None);

    [Benchmark]
    public Task MediatR_5NotificationHandlers()
        => _mediatorForNotifications.Publish(_mediatRNotification, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _nexumSp.Dispose();
        _nexumPipelineSp.Dispose();
        _nexumNotifSp.Dispose();
        _mediatRSp.Dispose();
        _mediatRPipelineSp.Dispose();
        _mediatRNotifSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
