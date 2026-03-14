using Nexum.Abstractions;

namespace Nexum.Testing;

/// <summary>
/// Fluent setup builder for configuring the behavior of <see cref="FakeQueryDispatcher"/>
/// for a specific query type.
/// </summary>
/// <typeparam name="TQuery">The query type.</typeparam>
/// <typeparam name="TResult">The result type produced by the query.</typeparam>
public sealed class FakeQuerySetup<TQuery, TResult> where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Gets the configured handler delegate, or <c>null</c> if none has been set.
    /// </summary>
    internal Func<TQuery, CancellationToken, ValueTask<TResult>>? Handler { get; private set; }

    /// <summary>
    /// Configures the dispatcher to return the specified result for this query type.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeQuerySetup<TQuery, TResult> Returns(TResult result)
    {
        Handler = (_, _) => ValueTask.FromResult(result);
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to invoke the specified factory to produce a result.
    /// </summary>
    /// <param name="factory">A function that receives the query and returns the result.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeQuerySetup<TQuery, TResult> Returns(Func<TQuery, TResult> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Handler = (q, _) => ValueTask.FromResult(factory(q));
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to invoke the specified asynchronous factory to produce a result.
    /// </summary>
    /// <param name="factory">An async function that receives the query and cancellation token.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeQuerySetup<TQuery, TResult> ReturnsAsync(Func<TQuery, CancellationToken, ValueTask<TResult>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Handler = factory;
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to throw an exception of <typeparamref name="TException"/>
    /// (created via parameterless constructor) when this query is dispatched.
    /// </summary>
    /// <typeparam name="TException">The exception type to throw.</typeparam>
    /// <returns>This setup instance for chaining.</returns>
    public FakeQuerySetup<TQuery, TResult> Throws<TException>() where TException : Exception, new()
    {
        Handler = (_, _) => throw new TException();
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to throw the specified exception when this query is dispatched.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeQuerySetup<TQuery, TResult> Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Handler = (_, _) => throw exception;
        return this;
    }
}

/// <summary>
/// Fluent setup builder for configuring the streaming behavior of <see cref="FakeQueryDispatcher"/>
/// for a specific stream query type.
/// </summary>
/// <typeparam name="TQuery">The stream query type.</typeparam>
/// <typeparam name="TResult">The type of each element in the result stream.</typeparam>
public sealed class FakeStreamSetup<TQuery, TResult> where TQuery : IStreamQuery<TResult>
{
    /// <summary>
    /// Gets the configured stream handler delegate, or <c>null</c> if none has been set.
    /// </summary>
    internal Func<TQuery, CancellationToken, IAsyncEnumerable<TResult>>? Handler { get; private set; }

    /// <summary>
    /// Configures the dispatcher to return the specified async enumerable stream for this query type.
    /// </summary>
    /// <param name="stream">The stream to return.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeStreamSetup<TQuery, TResult> Returns(IAsyncEnumerable<TResult> stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Handler = (_, _) => stream;
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to invoke the specified factory to produce a stream.
    /// </summary>
    /// <param name="factory">A function that receives the query and returns a stream.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeStreamSetup<TQuery, TResult> Returns(Func<TQuery, IAsyncEnumerable<TResult>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        Handler = (q, _) => factory(q);
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to yield the specified items as a stream for this query type.
    /// </summary>
    /// <param name="items">The items to yield.</param>
    /// <returns>This setup instance for chaining.</returns>
    public FakeStreamSetup<TQuery, TResult> Returns(params TResult[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Handler = (_, ct) => ToAsyncEnumerableAsync(items, ct);
        return this;
    }

    /// <summary>
    /// Configures the dispatcher to throw an exception of <typeparamref name="TException"/>
    /// (created via parameterless constructor) when this stream query is dispatched.
    /// </summary>
    /// <typeparam name="TException">The exception type to throw.</typeparam>
    /// <returns>This setup instance for chaining.</returns>
    public FakeStreamSetup<TQuery, TResult> Throws<TException>() where TException : Exception, new()
    {
        Handler = (_, _) => throw new TException();
        return this;
    }

    private static async IAsyncEnumerable<TResult> ToAsyncEnumerableAsync(
        TResult[] items,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            yield return item;
            await Task.Yield();
        }
    }
}
