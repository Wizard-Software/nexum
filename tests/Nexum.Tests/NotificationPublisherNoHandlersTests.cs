using System.Threading.Channels;
using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class NotificationPublisherNoHandlersTests : IDisposable
{
    private ServiceProvider? _serviceProvider;

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task PublishAsync_NoHandlersRegistered_CompletesWithoutExceptionAsync()
    {
        // Arrange
        var (publisher, sp) = CreatePublisher();
        _serviceProvider = sp;

        var notification = new TestNotification();

        // Act & Assert — should complete without throwing
        await publisher.PublishAsync(notification, ct: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task PublishAsync_NullStrategy_UsesDefaultStrategyAsync()
    {
        // Arrange
        var log = new List<string>();
        var (publisher, sp) = CreatePublisher(services =>
        {
            services.AddScoped<INotificationHandler<TestNotification>>(_ =>
                new TrackingNotificationHandler(log));
        });
        _serviceProvider = sp;

        var notification = new TestNotification("default-strategy");

        // Act — strategy: null → NexumOptions.DefaultPublishStrategy (Sequential)
        await publisher.PublishAsync(notification, strategy: null, ct: TestContext.Current.CancellationToken);

        // Assert — handler was invoked (proving Sequential strategy was used)
        log.Should().BeEquivalentTo(["Handler:default-strategy"], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task PublishAsync_NullNotification_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        var (publisher, sp) = CreatePublisher();
        _serviceProvider = sp;

        // Act & Assert
        var act = async () => await publisher.PublishAsync<TestNotification>(null!, ct: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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

    internal sealed class TrackingNotificationHandler(List<string> log)
        : INotificationHandler<TestNotification>
    {
        public ValueTask HandleAsync(TestNotification notification, CancellationToken ct = default)
        {
            log.Add($"Handler:{notification.Value}");
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
