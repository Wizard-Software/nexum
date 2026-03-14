using System.Collections.Concurrent;
using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// A fake implementation of <see cref="ICommandDispatcher"/> for use in tests.
/// Supports fluent setup of expected results and records all dispatched commands for later verification.
/// </summary>
/// <remarks>
/// This class is thread-safe. Use <see cref="Setup{TCommand, TResult}"/> to configure behavior before
/// dispatching. If no setup is found for a command type, an <see cref="InvalidOperationException"/> is thrown.
/// <see cref="DispatchedCommands"/> returns a point-in-time snapshot.
/// </remarks>
public sealed class FakeCommandDispatcher : ICommandDispatcher
{
    private readonly ConcurrentDictionary<Type, object> _setups = new();
    private readonly ConcurrentDictionary<Type, Func<object, CancellationToken, ValueTask<object?>>> _dispatchers = new();
    private ConcurrentQueue<object> _dispatched = new();

    /// <summary>
    /// Configures the fake dispatcher behavior for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">The command type to configure.</typeparam>
    /// <typeparam name="TResult">The result type produced by the command.</typeparam>
    /// <returns>A <see cref="FakeCommandSetup{TCommand, TResult}"/> for fluent configuration.</returns>
    public FakeCommandSetup<TCommand, TResult> Setup<TCommand, TResult>() where TCommand : ICommand<TResult>
    {
        var setup = new FakeCommandSetup<TCommand, TResult>();
        _setups[typeof(TCommand)] = setup;
        _dispatchers[typeof(TCommand)] = async (cmd, ct) =>
        {
            var handler = setup.Handler
                ?? throw new InvalidOperationException(
                    $"No return value configured for command type '{typeof(TCommand).Name}'. " +
                    $"Call Setup<{typeof(TCommand).Name}, {typeof(TResult).Name}>().Returns(...) first.");

            var result = await handler((TCommand)cmd, ct).ConfigureAwait(false);
            return result;
        };

        return setup;
    }

    /// <inheritdoc />
    public ValueTask<TResult> DispatchAsync<TResult>(ICommand<TResult> command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        _dispatched.Enqueue(command);

        var commandType = command.GetType();
        if (!_dispatchers.TryGetValue(commandType, out var dispatcher))
        {
            throw new InvalidOperationException(
                $"No setup configured for command type '{commandType.Name}'. " +
                $"Call Setup<{commandType.Name}, {typeof(TResult).Name}>().Returns(...) first.");
        }

        return InvokeAsync<TResult>(dispatcher, command, ct);
    }

    /// <summary>
    /// Gets a snapshot of all commands dispatched since the last <see cref="Reset"/>, in dispatch order.
    /// </summary>
    public IReadOnlyList<object> DispatchedCommands => _dispatched.ToArray();

    /// <summary>
    /// Clears all configured setups and the dispatch history.
    /// </summary>
    public void Reset()
    {
        _setups.Clear();
        _dispatchers.Clear();
        _dispatched = new ConcurrentQueue<object>();
    }

    private static async ValueTask<TResult> InvokeAsync<TResult>(
        Func<object, CancellationToken, ValueTask<object?>> dispatcher,
        object command,
        CancellationToken ct)
    {
        var result = await dispatcher(command, ct).ConfigureAwait(false);
        return (TResult)result!;
    }
}
