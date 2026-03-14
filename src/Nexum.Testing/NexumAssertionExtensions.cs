using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// Extension methods for verifying dispatched commands, queries, and published notifications.
/// </summary>
public static class NexumAssertionExtensions
{
    // === Command assertions (on FakeCommandDispatcher) ===

    /// <summary>
    /// Verifies that at least one command of type <typeparamref name="TCommand"/> was dispatched.
    /// </summary>
    /// <typeparam name="TCommand">The command type to verify.</typeparam>
    /// <param name="dispatcher">The fake dispatcher to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no command of type <typeparamref name="TCommand"/> was dispatched.
    /// </exception>
    public static void ShouldHaveDispatched<TCommand>(this FakeCommandDispatcher dispatcher)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var matches = dispatcher.DispatchedCommands.OfType<TCommand>().ToList();
        if (matches.Count == 0)
        {
            throw new NexumAssertionException(
                $"Expected at least one dispatch of '{typeof(TCommand).Name}', " +
                $"but none was found. Dispatched commands: [{FormatList(dispatcher.DispatchedCommands)}]");
        }
    }

    /// <summary>
    /// Verifies that at least one command of type <typeparamref name="TCommand"/> matching the predicate was dispatched.
    /// </summary>
    /// <typeparam name="TCommand">The command type to verify.</typeparam>
    /// <param name="dispatcher">The fake dispatcher to inspect.</param>
    /// <param name="predicate">A predicate to filter dispatched commands of the given type.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no command of type <typeparamref name="TCommand"/> matching the predicate was dispatched.
    /// </exception>
    public static void ShouldHaveDispatched<TCommand>(this FakeCommandDispatcher dispatcher, Func<TCommand, bool> predicate)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = dispatcher.DispatchedCommands.OfType<TCommand>().Where(predicate).ToList();
        if (matches.Count == 0)
        {
            var typeMatches = dispatcher.DispatchedCommands.OfType<TCommand>().Cast<object>().ToList();
            throw new NexumAssertionException(
                $"Expected at least one dispatch of '{typeof(TCommand).Name}' matching the predicate, " +
                $"but none was found. Dispatched '{typeof(TCommand).Name}' commands: [{FormatList(typeMatches)}]");
        }
    }

    /// <summary>
    /// Verifies that exactly <paramref name="times"/> commands of type <typeparamref name="TCommand"/> were dispatched.
    /// </summary>
    /// <typeparam name="TCommand">The command type to verify.</typeparam>
    /// <param name="dispatcher">The fake dispatcher to inspect.</param>
    /// <param name="times">The exact number of expected dispatches.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when the number of dispatched commands of type <typeparamref name="TCommand"/> does not equal <paramref name="times"/>.
    /// </exception>
    public static void ShouldHaveDispatched<TCommand>(this FakeCommandDispatcher dispatcher, int times)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var count = dispatcher.DispatchedCommands.OfType<TCommand>().Count();
        if (count != times)
        {
            throw new NexumAssertionException(
                $"Expected {times} dispatch(es) of '{typeof(TCommand).Name}', but found {count}.");
        }
    }

    /// <summary>
    /// Verifies that no command of type <typeparamref name="TCommand"/> was dispatched.
    /// </summary>
    /// <typeparam name="TCommand">The command type to verify was not dispatched.</typeparam>
    /// <param name="dispatcher">The fake dispatcher to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when at least one command of type <typeparamref name="TCommand"/> was dispatched.
    /// </exception>
    public static void ShouldNotHaveDispatched<TCommand>(this FakeCommandDispatcher dispatcher)
        where TCommand : ICommand
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var matches = dispatcher.DispatchedCommands.OfType<TCommand>().ToList();
        if (matches.Count > 0)
        {
            throw new NexumAssertionException(
                $"Expected no dispatch of '{typeof(TCommand).Name}', " +
                $"but found {matches.Count} dispatch(es): [{FormatList(matches.Cast<object>())}]");
        }
    }

    // === Query assertions (on FakeQueryDispatcher) ===

    /// <summary>
    /// Verifies that at least one query of type <typeparamref name="TQuery"/> was dispatched.
    /// </summary>
    /// <typeparam name="TQuery">The query type to verify.</typeparam>
    /// <param name="dispatcher">The fake query dispatcher to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no query of type <typeparamref name="TQuery"/> was dispatched.
    /// </exception>
    public static void ShouldHaveDispatched<TQuery>(this FakeQueryDispatcher dispatcher)
        where TQuery : IQuery
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var matches = dispatcher.DispatchedQueries.OfType<TQuery>().ToList();
        if (matches.Count == 0)
        {
            throw new NexumAssertionException(
                $"Expected at least one dispatch of '{typeof(TQuery).Name}', " +
                $"but none was found. Dispatched queries: [{FormatList(dispatcher.DispatchedQueries)}]");
        }
    }

    /// <summary>
    /// Verifies that at least one query of type <typeparamref name="TQuery"/> matching the predicate was dispatched.
    /// </summary>
    /// <typeparam name="TQuery">The query type to verify.</typeparam>
    /// <param name="dispatcher">The fake query dispatcher to inspect.</param>
    /// <param name="predicate">A predicate to filter dispatched queries of the given type.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no query of type <typeparamref name="TQuery"/> matching the predicate was dispatched.
    /// </exception>
    public static void ShouldHaveDispatched<TQuery>(this FakeQueryDispatcher dispatcher, Func<TQuery, bool> predicate)
        where TQuery : IQuery
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = dispatcher.DispatchedQueries.OfType<TQuery>().Where(predicate).ToList();
        if (matches.Count == 0)
        {
            var typeMatches = dispatcher.DispatchedQueries.OfType<TQuery>().Cast<object>().ToList();
            throw new NexumAssertionException(
                $"Expected at least one dispatch of '{typeof(TQuery).Name}' matching the predicate, " +
                $"but none was found. Dispatched '{typeof(TQuery).Name}' queries: [{FormatList(typeMatches)}]");
        }
    }

    /// <summary>
    /// Verifies that no query of type <typeparamref name="TQuery"/> was dispatched.
    /// </summary>
    /// <typeparam name="TQuery">The query type to verify was not dispatched.</typeparam>
    /// <param name="dispatcher">The fake query dispatcher to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when at least one query of type <typeparamref name="TQuery"/> was dispatched.
    /// </exception>
    public static void ShouldNotHaveDispatched<TQuery>(this FakeQueryDispatcher dispatcher)
        where TQuery : IQuery
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        var matches = dispatcher.DispatchedQueries.OfType<TQuery>().ToList();
        if (matches.Count > 0)
        {
            throw new NexumAssertionException(
                $"Expected no dispatch of '{typeof(TQuery).Name}', " +
                $"but found {matches.Count} dispatch(es): [{FormatList(matches.Cast<object>())}]");
        }
    }

    // === Notification assertions (on InMemoryNotificationCollector) ===

    /// <summary>
    /// Verifies that at least one notification of type <typeparamref name="TNotification"/> was published.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to verify.</typeparam>
    /// <param name="collector">The notification collector to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no notification of type <typeparamref name="TNotification"/> was published.
    /// </exception>
    public static void ShouldHavePublished<TNotification>(this InMemoryNotificationCollector collector)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(collector);

        var matches = collector.PublishedNotifications.OfType<TNotification>().ToList();
        if (matches.Count == 0)
        {
            throw new NexumAssertionException(
                $"Expected at least one published notification of type '{typeof(TNotification).Name}', " +
                $"but none was found. Published notifications: [{FormatList(collector.PublishedNotifications)}]");
        }
    }

    /// <summary>
    /// Verifies that at least one notification of type <typeparamref name="TNotification"/> matching the predicate was published.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to verify.</typeparam>
    /// <param name="collector">The notification collector to inspect.</param>
    /// <param name="predicate">A predicate to filter published notifications of the given type.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when no notification of type <typeparamref name="TNotification"/> matching the predicate was published.
    /// </exception>
    public static void ShouldHavePublished<TNotification>(this InMemoryNotificationCollector collector, Func<TNotification, bool> predicate)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(collector);
        ArgumentNullException.ThrowIfNull(predicate);

        var matches = collector.PublishedNotifications.OfType<TNotification>().Where(predicate).ToList();
        if (matches.Count == 0)
        {
            var typeMatches = collector.PublishedNotifications.OfType<TNotification>().Cast<object>().ToList();
            throw new NexumAssertionException(
                $"Expected at least one published notification of type '{typeof(TNotification).Name}' matching the predicate, " +
                $"but none was found. Published '{typeof(TNotification).Name}' notifications: [{FormatList(typeMatches)}]");
        }
    }

    /// <summary>
    /// Verifies that no notification of type <typeparamref name="TNotification"/> was published.
    /// </summary>
    /// <typeparam name="TNotification">The notification type to verify was not published.</typeparam>
    /// <param name="collector">The notification collector to inspect.</param>
    /// <exception cref="NexumAssertionException">
    /// Thrown when at least one notification of type <typeparamref name="TNotification"/> was published.
    /// </exception>
    public static void ShouldNotHavePublished<TNotification>(this InMemoryNotificationCollector collector)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(collector);

        var matches = collector.PublishedNotifications.OfType<TNotification>().ToList();
        if (matches.Count > 0)
        {
            throw new NexumAssertionException(
                $"Expected no published notification of type '{typeof(TNotification).Name}', " +
                $"but found {matches.Count} notification(s): [{FormatList(matches.Cast<object>())}]");
        }
    }

    private static string FormatList(IEnumerable<object> items)
        => string.Join(", ", items.Select(i => i.ToString()));
}
