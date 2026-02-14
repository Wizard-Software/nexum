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
public class NotificationBenchmarks
{
    private INotificationPublisher _publisher = null!;
    private ServiceProvider _sp = null!;
    private readonly BenchNotification _notification = new("bench");

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var options = new NexumOptions();
        services.AddSingleton(options);
        services.AddSingleton<ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();

        // Create a channel for FireAndForget (required by NotificationPublisher ctor)
        var channel = Channel.CreateBounded<NotificationEnvelope>(1000);
        services.AddSingleton<INotificationPublisher>(sp =>
            new NotificationPublisher(sp, sp.GetRequiredService<NexumOptions>(), channel.Writer));

        services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler1>();
        services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler2>();
        services.AddScoped<INotificationHandler<BenchNotification>, BenchNotificationHandler3>();

        _sp = services.BuildServiceProvider();
        _publisher = _sp.GetRequiredService<INotificationPublisher>();

        // Warm up
        _publisher.PublishAsync(_notification, PublishStrategy.Sequential, CancellationToken.None)
            .AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask PublishNotification_3Handlers_Sequential()
        => _publisher.PublishAsync(_notification, PublishStrategy.Sequential, CancellationToken.None);

    [Benchmark]
    public ValueTask PublishNotification_3Handlers_Parallel()
        => _publisher.PublishAsync(_notification, PublishStrategy.Parallel, CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _sp.Dispose();
    }
}
