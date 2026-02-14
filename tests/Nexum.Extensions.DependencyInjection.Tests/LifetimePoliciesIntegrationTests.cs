using Nexum.Abstractions;
using Nexum.Extensions.DependencyInjection.Tests.TestFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Nexum.Extensions.DependencyInjection.Tests;

[Trait("Category", "Integration")]
public sealed class LifetimePoliciesIntegrationTests
{
    [Fact]
    public void AddNexum_HandlersRegisteredAsScoped_ByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexum(assemblies: typeof(LifetimeHandler).Assembly);

        // Act & Assert — find the handler registration
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ICommandHandler<LifetimeCommand, string>)
            && d.ImplementationType == typeof(LifetimeHandler));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddNexum_BehaviorsRegisteredAsTransient_ByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexumBehavior(typeof(TrackingBehaviorA<,>));

        // Act & Assert
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ICommandBehavior<,>)
            && d.ImplementationType == typeof(TrackingBehaviorA<,>));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddNexum_DispatchersRegisteredAsSingleton_ByDefault()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexum(assemblies: typeof(LifetimeHandler).Assembly);

        // Act & Assert — check ICommandDispatcher
        var commandDispatcher = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ICommandDispatcher));
        commandDispatcher.Should().NotBeNull();
        commandDispatcher!.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Check IQueryDispatcher
        var queryDispatcher = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IQueryDispatcher));
        queryDispatcher.Should().NotBeNull();
        queryDispatcher!.Lifetime.Should().Be(ServiceLifetime.Singleton);

        // Check INotificationPublisher — uses factory, so only check Lifetime (not ImplementationType)
        var notificationPublisher = services.FirstOrDefault(d =>
            d.ServiceType == typeof(INotificationPublisher));
        notificationPublisher.Should().NotBeNull();
        notificationPublisher!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddNexum_HandlerLifetimeAttribute_OverridesDefaultScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNexum(assemblies: typeof(SingletonLifetimeHandler).Assembly);

        // Act & Assert — SingletonLifetimeHandler should be registered as Singleton
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ICommandHandler<LifetimeCommand, string>)
            && d.ImplementationType == typeof(SingletonLifetimeHandler));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
