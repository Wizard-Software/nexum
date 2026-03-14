using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// Fluent setup builder for configuring the behavior of <see cref="FakeCommandDispatcher"/>
/// for a specific command type.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type produced by the command.</typeparam>
public sealed class FakeCommandSetup<TCommand, TResult> where TCommand : ICommand<TResult>
{
    /// <summary>
    /// Gets the configured handler delegate, or <c>null</c> if none has been set.
    /// </summary>
    internal Func<TCommand, CancellationToken, ValueTask<TResult>>? Handler { get; private set; }

    /// <summary>
    /// Configures the dispatcher to return the specified result for this command type.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeCommandSetup<TCommand, TResult> Returns(TResult result)
    {
        Handler = (_, _) => ValueTask.FromResult(result);
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to invoke the specified factory to produce a result.
    /// </summary>
    /// <param name="factory">A function that receives the command and returns the result.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeCommandSetup<TCommand, TResult> Returns(Func<TCommand, TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Handler = (cmd, _) => ValueTask.FromResult(factory(cmd));
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to invoke the specified asynchronous factory to produce a result.
    /// </summary>
    /// <param name="factory">An async function that receives the command and cancellation token.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeCommandSetup<TCommand, TResult> ReturnsAsync(Func<TCommand, CancellationToken, ValueTask<TResult>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Handler = factory;
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to throw an exception of <typeparamref name="TException"/>
    /// (created via parameterless constructor) when this command is dispatched.
    /// </summary>
    /// <typeparam name="TException">The exception type to throw.</typeparam>
    /// <returns>This setup instance for chaining.</returns>
    public FakeCommandSetup<TCommand, TResult> Throws<TException>() where TException : Exception, new()
    {
        Handler = (_, _) => throw new TException();
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to throw the specified exception when this command is dispatched.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeCommandSetup<TCommand, TResult> Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Handler = (_, _) => throw exception;
        return this;
    }
}
