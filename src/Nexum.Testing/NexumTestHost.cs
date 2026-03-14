using Nexum.Abstractions;
using Nexum.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// Lightweight in-memory host for testing Nexum command/query/notification dispatching.
/// Registers core Nexum infrastructure (dispatchers, options, logging) and allows adding
/// handlers and behaviors for testing without requiring assembly scanning or Source Generators.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="NexumTestHost"/> wires up the full Nexum dispatch pipeline — including
/// <see cref="CommandDispatcher"/>, <see cref="QueryDispatcher"/>, and an
/// <see cref="InMemoryNotificationCollector"/> — using a standalone <see cref="IServiceCollection"/>.
/// </para>
/// <para>
/// Use <see cref="Create"/> with a builder callback for one-line setup:
/// </para>
/// <code>
/// using var host = NexumTestHost.Create(b => b.AddHandler&lt;MyCommandHandler&gt;());
/// var result = await host.CommandDispatcher.DispatchAsync(new MyCommand("x"));
/// </code>
/// <para>
/// The host implements <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>. Disposing the host
/// releases the underlying <see cref="IServiceProvider"/> and resets all dispatcher caches to prevent
/// cross-test contamination.
/// </para>
/// </remarks>
public sealed class NexumTestHost : IDisposable, IAsyncDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly InMemoryNotificationCollector? _notificationCollector;

    private NexumTestHost(ServiceProvider serviceProvider, InMemoryNotificationCollector? collector)
    {
        _serviceProvider = serviceProvider;
        _notificationCollector = collector;
        CommandDispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        QueryDispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();
        NotificationPublisher = serviceProvider.GetRequiredService<INotificationPublisher>();
        Services = serviceProvider;
    }

    /// <summary>
    /// Creates a new <see cref="NexumTestHost"/> using the provided builder configuration.
    /// </summary>
    /// <param name="configure">A callback that configures the <see cref="NexumTestHostBuilder"/>.</param>
    /// <returns>A fully initialized <see cref="NexumTestHost"/> ready for dispatching.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is null.</exception>
    public static NexumTestHost Create(Action<NexumTestHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new NexumTestHostBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>
    /// Gets the command dispatcher for dispatching <see cref="ICommand{TResult}"/> instances.
    /// </summary>
    public ICommandDispatcher CommandDispatcher { get; }

    /// <summary>
    /// Gets the query dispatcher for dispatching <see cref="IQuery{TResult}"/> instances.
    /// </summary>
    public IQueryDispatcher QueryDispatcher { get; }

    /// <summary>
    /// Gets the notification publisher. When <see cref="NexumTestHostBuilder.UseNotificationCollector"/>
    /// is used (the default), this is the same instance as <see cref="NotificationCollector"/>.
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; }

    /// <summary>
    /// Gets the underlying <see cref="IServiceProvider"/> for resolving any registered services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Returns the <see cref="InMemoryNotificationCollector"/> if <see cref="NexumTestHostBuilder.UseNotificationCollector"/>
    /// was called on the builder (which is the default). Returns <see langword="null"/> if a custom
    /// <see cref="INotificationPublisher"/> was registered via <see cref="NexumTestHostBuilder.ConfigureServices"/>.
    /// </summary>
    public InMemoryNotificationCollector? NotificationCollector => _notificationCollector;

    /// <inheritdoc />
    public void Dispose()
    {
        _serviceProvider.Dispose();
        PolymorphicHandlerResolver.ResetForTesting();
        Nexum.CommandDispatcher.ResetForTesting();
        Nexum.QueryDispatcher.ResetForTesting();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        PolymorphicHandlerResolver.ResetForTesting();
        Nexum.CommandDispatcher.ResetForTesting();
        Nexum.QueryDispatcher.ResetForTesting();
    }

    internal static NexumTestHost BuildFrom(ServiceProvider serviceProvider, InMemoryNotificationCollector? collector)
        => new(serviceProvider, collector);
}

/// <summary>
/// Fluent builder for configuring and creating a <see cref="NexumTestHost"/>.
/// </summary>
/// <remarks>
/// Handlers are registered as Scoped services by default, which matches the lifetime recommended by
/// the Nexum architecture guidelines. Behaviors are also registered as Scoped. The builder pre-configures
/// <see cref="NexumOptions"/>, <see cref="ExceptionHandlerResolver"/>, <see cref="CommandDispatcher"/>,
/// <see cref="QueryDispatcher"/>, and (by default) an <see cref="InMemoryNotificationCollector"/>.
/// </remarks>
public sealed class NexumTestHostBuilder
{
    private readonly List<Action<IServiceCollection>> _serviceActions = [];
    private readonly List<Action<NexumOptions>> _configureOptionsActions = [];
    private bool _useNotificationCollector;

    /// <summary>
    /// Initializes a new <see cref="NexumTestHostBuilder"/> with the notification collector enabled by default.
    /// </summary>
    public NexumTestHostBuilder()
    {
        _useNotificationCollector = true;
    }

    /// <summary>
    /// Registers a handler by its concrete type with Scoped lifetime.
    /// </summary>
    /// <typeparam name="THandler">The concrete handler type (e.g., <c>MyCommandHandler</c>).</typeparam>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddHandler<THandler>() where THandler : class
    {
        _serviceActions.Add(services => services.AddScoped<THandler>());
        return this;
    }

    /// <summary>
    /// Registers a handler with an explicit service type and implementation type with Scoped lifetime.
    /// </summary>
    /// <typeparam name="TService">The handler interface type (e.g., <c>ICommandHandler&lt;MyCommand, string&gt;</c>).</typeparam>
    /// <typeparam name="TImplementation">The concrete implementation type.</typeparam>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddHandler<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        _serviceActions.Add(services => services.AddScoped<TService, TImplementation>());
        return this;
    }

    /// <summary>
    /// Registers a handler as a singleton using the provided instance.
    /// </summary>
    /// <typeparam name="TService">The handler interface type.</typeparam>
    /// <param name="instance">The pre-created handler instance.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddHandler<TService>(TService instance) where TService : class
    {
        _serviceActions.Add(services => services.AddSingleton(instance));
        return this;
    }

    /// <summary>
    /// Registers a handler using a factory delegate with Scoped lifetime.
    /// </summary>
    /// <typeparam name="TService">The handler interface type.</typeparam>
    /// <param name="factory">A factory that creates the handler from the <see cref="IServiceProvider"/>.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddHandler<TService>(Func<IServiceProvider, TService> factory) where TService : class
    {
        _serviceActions.Add(services => services.AddScoped(factory));
        return this;
    }

    /// <summary>
    /// Registers a behavior by its concrete type with Scoped lifetime.
    /// Behaviors execute in registration order.
    /// </summary>
    /// <typeparam name="TBehavior">The concrete behavior type.</typeparam>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddBehavior<TBehavior>() where TBehavior : class
    {
        _serviceActions.Add(services => services.AddScoped<TBehavior>());
        return this;
    }

    /// <summary>
    /// Registers a behavior by its <see cref="Type"/> with Scoped lifetime.
    /// Behaviors execute in registration order.
    /// </summary>
    /// <param name="behaviorType">The behavior type to register.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder AddBehavior(Type behaviorType)
    {
        ArgumentNullException.ThrowIfNull(behaviorType);
        _serviceActions.Add(services => services.AddScoped(behaviorType));
        return this;
    }

    /// <summary>
    /// Applies a configuration callback to <see cref="NexumOptions"/> before the host is built.
    /// </summary>
    /// <param name="configure">A callback that mutates the <see cref="NexumOptions"/> instance.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder Configure(Action<NexumOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _configureOptionsActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Provides direct access to the underlying <see cref="IServiceCollection"/> for advanced scenarios.
    /// </summary>
    /// <param name="configure">A callback that mutates the service collection.</param>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _serviceActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Ensures that an <see cref="InMemoryNotificationCollector"/> is registered as the
    /// <see cref="INotificationPublisher"/>. This is the default behavior — calling this
    /// method explicitly is only needed when overriding a previous custom registration.
    /// </summary>
    /// <returns>This builder instance for chaining.</returns>
    public NexumTestHostBuilder UseNotificationCollector()
    {
        _useNotificationCollector = true;
        return this;
    }

    internal NexumTestHost Build()
    {
        var services = new ServiceCollection();

        // Core infrastructure: options
        var options = new NexumOptions();
        foreach (Action<NexumOptions> action in _configureOptionsActions)
        {
            action(options);
        }

        services.AddSingleton(options);

        // Logging — NullLogger avoids any real logging noise in tests
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        // Internal infrastructure
        services.AddSingleton<ExceptionHandlerResolver>();

        // Dispatchers (singletons — thread-safe by design)
        services.AddSingleton<ICommandDispatcher, CommandDispatcher>();
        services.AddSingleton<IQueryDispatcher, QueryDispatcher>();

        // Notification publisher
        InMemoryNotificationCollector? collector = null;
        if (_useNotificationCollector)
        {
            collector = new InMemoryNotificationCollector();
            services.AddSingleton<INotificationPublisher>(collector);
        }

        // Apply user-registered handlers, behaviors, and custom services
        foreach (Action<IServiceCollection> action in _serviceActions)
        {
            action(services);
        }

        ServiceProvider sp = services.BuildServiceProvider();
        return NexumTestHost.BuildFrom(sp, collector);
    }
}
