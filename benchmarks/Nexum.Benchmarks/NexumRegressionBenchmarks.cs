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
[MediumRunJob(RuntimeMoniker.Net10_0)]
[JsonExporterAttribute.FullCompressed]
public class NexumRegressionBenchmarks
{
    private ICommandDispatcher _nexumDispatcher = null!;
    private ICommandDispatcher _nexumPipelineDispatcher = null!;
    private INotificationPublisher _nexumNotificationPublisher = null!;
    private ServiceProvider _nexumSp = null!;
    private ServiceProvider _nexumPipelineSp = null!;
    private ServiceProvider _nexumNotifSp = null!;

    private readonly BenchCommand _nexumCommand = new("bench");
    private readonly BenchNotification _nexumNotification = new("bench");

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

        // Reset static caches
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();

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
    }

    [Benchmark(Baseline = true)]
    public ValueTask<Guid> Nexum_SimpleCommand()
        => _nexumDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public ValueTask<Guid> Nexum_3Behaviors()
        => _nexumPipelineDispatcher.DispatchAsync(_nexumCommand, CancellationToken.None);

    [Benchmark]
    public ValueTask Nexum_5NotificationHandlers()
        => _nexumNotificationPublisher.PublishAsync(_nexumNotification, PublishStrategy.Sequential, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _nexumSp.Dispose();
        _nexumPipelineSp.Dispose();
        _nexumNotifSp.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        CommandDispatcher.ResetForTesting();
        PipelineBuilder.ResetForTesting();
    }
}
