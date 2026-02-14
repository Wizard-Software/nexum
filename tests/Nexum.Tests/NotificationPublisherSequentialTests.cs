using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class NotificationPublisherSequentialTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task PublishAsync_Sequential_ThreeHandlers_AllInvokedAsync()
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
        await publisher.PublishAsync(notification, PublishStrategy.Sequential, TestContext.Current.CancellationToken);

        // Assert — all three handlers invoked in registration order
        log.Should().BeEquivalentTo(["H1:test", "H2:test", "H3:test"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PublishAsync_Sequential_OneHandlerThrows_ThrowsDirectlyAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H1"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("handler error")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H3"));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act & Assert — single exception thrown directly (not AggregateException)
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Sequential, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("handler error");
    }

    [Fact]
    public async Task PublishAsync_Sequential_TwoHandlersThrow_ThrowsAggregateExceptionAsync()
    {
        // Arrange
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("error1")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new ArgumentException("error2")));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act & Assert — ≥2 exceptions → AggregateException
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Sequential, TestContext.Current.CancellationToken);
        var ex = await act.Should().ThrowAsync<AggregateException>();
        ex.Which.InnerExceptions.Should().HaveCount(2);
        ex.Which.InnerExceptions[0].Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("error1");
        ex.Which.InnerExceptions[1].Should().BeOfType<ArgumentException>()
            .Which.Message.Should().Be("error2");
    }

    [Fact]
    public async Task PublishAsync_Sequential_HandlerThrows_ExceptionHandlerInvokedAsync()
    {
        // Arrange
        var exceptionHandlerLog = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("test error")));
            services.AddTransient<INotificationExceptionHandler<TestNotification, InvalidOperationException>>(_ =>
                new TrackingExceptionHandler<TestNotification, InvalidOperationException>(exceptionHandlerLog));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Sequential, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert — exception handler was invoked
        exceptionHandlerLog.Should().ContainSingle()
            .Which.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task PublishAsync_Sequential_MiddleHandlerThrows_AllHandlersStillInvokedAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H1"));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new ThrowingNotificationHandler(new InvalidOperationException("error")));
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log, "H3"));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("test");

        // Act — exception propagated, but all handlers were invoked
        var act = async () => await publisher.PublishAsync(
            notification, PublishStrategy.Sequential, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert — H1 and H3 were invoked despite H2 throwing
        log.Should().BeEquivalentTo(["H1:test", "H3:test"], options => options.WithStrictOrdering());
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
