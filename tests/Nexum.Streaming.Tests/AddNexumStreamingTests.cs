#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode for test usage
#pragma warning disable IL2067 // Suppress RequiresDynamicCode for test usage

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexum.Abstractions;

namespace Nexum.Streaming.Tests;

[Trait("Category", "Integration")]
public sealed class AddNexumStreamingTests
{
    [Fact]
    public void AddNexumStreaming_RegistersStreamNotificationPublisher()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexumStreaming();

        // Act
        var sp = services.BuildServiceProvider();
        var publisher = sp.GetService<IStreamNotificationPublisher>();

        // Assert
        publisher.Should().NotBeNull();
        publisher.Should().BeOfType<StreamNotificationPublisher>();
    }

    [Fact]
    public void AddNexumStreaming_WithOptions_ConfiguresOptions()
    {
        // Arrange
        int expectedCapacity = 512;

        var services = new ServiceCollection();
        services.AddNexumStreaming(opts => opts.MergeChannelCapacity = expectedCapacity);

        // Act
        var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<IOptions<NexumStreamingOptions>>();

        // Assert
        options.Value.MergeChannelCapacity.Should().Be(expectedCapacity);
    }

    [Fact]
    public void AddNexumStreaming_PublisherIsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexumStreaming();

        // Act
        var sp = services.BuildServiceProvider();
        var instance1 = sp.GetRequiredService<IStreamNotificationPublisher>();
        var instance2 = sp.GetRequiredService<IStreamNotificationPublisher>();

        // Assert — singleton: both resolutions return the same instance
        instance1.Should().BeSameAs(instance2);
    }
}
