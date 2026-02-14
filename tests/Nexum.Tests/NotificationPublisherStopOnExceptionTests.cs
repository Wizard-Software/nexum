using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class NotificationPublisherStopOnExceptionTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task PublishAsync_StopOnException_ThreeHandlers_AllInvokedAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H1"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H2"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H3"));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act
        await publisher.PublishAsync(notification, PublishStrategy.StopOnException, TestContext.Current.CancellationToken);

        // Assert — all three handlers invoked sequentially
        log.Should().BeEquivalentTo(["H1:test", "H2:test", "H3:test"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PublishAsync_StopOnException_FirstHandlerThrows_RemainingNotInvokedAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("stop")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H2"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H3"));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act & Assert — first handler throws, remaining not invoked
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.StopOnException, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("stop");

        // H2 and H3 should NOT have been invoked
        log.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_StopOnException_HandlerThrows_ExceptionHandlerInvokedAsync()
    {
        // Arrange
        var exceptionHandlerLog = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("stop error")));
            services.AddTransient<INotificationExceptionHandler<TestNotification, InvalidOperationException>>(_ =>
                new TrackingExceptionHandler<TestNotification, InvalidOperationException>(exceptionHandlerLog));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.StopOnException, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert — exception handler was invoked before propagation
        exceptionHandlerLog.Should().ContainSingle()
            .Which.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task PublishAsync_StopOnException_SecondHandlerThrows_FirstCompletedSecondStopsAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H1"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("stop at H2")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H3"));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.StopOnException, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("stop at H2");

        // Assert — H1 completed, H3 was not invoked
        log.Should().BeEquivalentTo(["H1:test"], options => options.WithStrictOrdering());
    }

    #region Helper Methods

    private static (NotificationPublisher Publisher, ServiceProvider ServiceProvider) CreatePublisher(
        Action<IServiceCollection>? configure = null)
    {
        var channel = Channel.CreateBounded<NotificationEnvelope>(100);
        var services = new ServiceCollection();
        services.AddSingleton(new NexumOptions());
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExceptionHandlerResolver>>(NullLogger<ExceptionHandlerResolver>.Instance);
        services.AddSingleton<ExceptionHandlerResolver>();
        services.AddSingleton(channel.Writer);
        configure?.Invoke(services);
        var sp = services.BuildServiceProvider();
        var publisher = new NotificationPublisher(sp, sp.GetRequiredService<NexumOptions>(), channel.Writer);
        return (publisher, sp);
    }

    #endregion

    #region Test Types

    internal sealed record TestNotification(string Value = "test") : INotification;

    internal sealed class TrackingNotificationHandler(List<string> log, string name)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            log.Add($"{name}:{notification.Value}");
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class ThrowingNotificationHandler(Exception exception)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
            => throw exception;
    }

    internal sealed class TrackingExceptionHandler<TNotification, TException>(List<string> log)
        : INotificationExceptionHandler<TNotification, TException>
        where TNotification : INotification
        where TException : Exception
    {
        public ValueTask HandleAsync(TNotification notification, TException exception, CancellationToken ct = default)
        {
            log.Add($"{typeof(TException).Name}:{exception.Message}");
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
