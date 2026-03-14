using System.Collections.Concurrent;
using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// A fake implementation of <see cref="IQueryDispatcher"/> for use in tests.
/// Supports fluent setup of expected results and streams, and records all dispatched queries
/// for later verification.
/// </summary>
/// <remarks>
/// This class is thread-safe. Use <see cref="Setup{TQuery, TResult}"/> for regular queries and
/// <see cref="SetupStream{TQuery, TResult}"/> for streaming queries. If no setup is found for a
/// query type, an <see cref="InvalidOperationException"/> is thrown.
/// <see cref="DispatchedQueries"/> returns a point-in-time snapshot.
/// </remarks>
public sealed class FakeQueryDispatcher : IQueryDispatcher
{
    private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, ValueTask<object?>>> _dispatchers = new();
    private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, object>> _streamDispatchers = new();
    private ConcurrentQueue<object> _dispatched = new();

    /// <summary>
    /// Configures the fake dispatcher behavior for the specified query type.
    /// </summary>
    /// <typeparam name="TQuery">The query type to configure.</typeparam>
    /// <typeparam name="TResult">The result type produced by the query.</typeparam>
    /// <returns>A <see cref="FakeQuerySetup{TQuery, TResult}"/> for fluent configuration.</returns>
    public FakeQuerySetup<TQuery, TResult> Setup<TQuery, TResult>() where TQuery : IQuery<TResult>
    {
        var setup = new FakeQuerySetup<TQuery, TResult>();
        _dispatchers[typeof(TQuery)] = async (q, ct) =>
        {
            var handler = setup.Handler
                ?? throw new InvalidOperationException(
                    $"No return value configured for query type '{typeof(TQuery).Name}'. " +
                    $"Call Setup<{typeof(TQuery).Name}, {typeof(TResult).Name}>().Returns(...) first.");

            var result = await handler((TQuery)q, ct).ConfigureAwait(false);
            return result;
        };

        return setup;
    }

    /// <summary>
    /// Configures the fake dispatcher streaming behavior for the specified stream query type.
    /// </summary>
    /// <typeparam name="TQuery">The stream query type to configure.</typeparam>
    /// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
    /// <returns>A <see cref="FakeStreamSetup{TQuery, TResult}"/> for fluent configuration.</returns>
    public FakeStreamSetup<TQuery, TResult> SetupStream<TQuery, TResult>() where TQuery : IStreamQuery<TResult>
    {
        var setup = new FakeStreamSetup<TQuery, TResult>();
        _streamDispatchers[typeof(TQuery)] = (q, ct) =>
        {
            var handler = setup.Handler
                ?? throw new InvalidOperationException(
                    $"No stream configured for stream query type '{typeof(TQuery).Name}'. " +
                    $"Call SetupStream<{typeof(TQuery).Name}, {typeof(TResult).Name}>().Returns(...) first.");

            return handler((TQuery)q, ct);
        };

        return setup;
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(IQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _dispatched.Enqueue(query);

        var queryType = query.GetType();
        if (!_dispatchers.TryGetValue(queryType, out var dispatcher))
        {
            throw new InvalidOperationException(
                $"No setup configured for query type '{queryType.Name}'. " +
                $"Call Setup<{queryType.Name}, {typeof(TResult).Name}>().Returns(...) first.");
        }

        return InvokeAsync<TResult>(dispatcher, query, ct);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(IStreamQuery<TResult> query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        _dispatched.Enqueue(query);

        var queryType = query.GetType();
        if (!_streamDispatchers.TryGetValue(queryType, out var dispatcher))
        {
            throw new InvalidOperationException(
                $"No setup configured for stream query type '{queryType.Name}'. " +
                $"Call SetupStream<{queryType.Name}, {typeof(TResult).Name}>().Returns(...) first.");
        }

        return (IAsyncEnumerable<TResult>)dispatcher(query, ct);
    }

    /// <summary>
    /// Gets a snapshot of all queries dispatched since the last <see cref="Reset"/>, in dispatch order.
    /// Includes both regular queries dispatched via <see cref="DispatchAsync{TResult}"/> and stream
    /// queries dispatched via <see cref="StreamAsync{TResult}"/>.
    /// </summary>
    public IReadOnlyList<object> DispatchedQueries => _dispatched.ToArray();

    /// <summary>
    /// Clears all configured setups and the dispatch history.
    /// </summary>
    public void Reset()
    {
        _dispatchers.Clear();
        _streamDispatchers.Clear();
        _dispatched = new ConcurrentQueue<object>();
    }

    private static async ValueTask<TResult> InvokeAsync<TResult>(
        Func<object, CancellationToken, ValueTask<object?>> dispatcher,
        object query,
        CancellationToken ct)
    {
        var result = await dispatcher(query, ct).ConfigureAwait(false);
        return (TResult)result!;
    }
}
