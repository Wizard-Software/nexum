using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Decorator that adds OpenTelemetry tracing and metrics to query dispatch and streaming.
/// Wraps the inner <see cref="IQueryDispatcher"/> with Activity spans and metric recording.
/// Also implements <see cref="IInterceptableDispatcher"/> to participate in Source Generator
/// Tier 3 intercepted dispatch paths.
/// </summary>
internal sealed class TracingQueryDispatcher : IQueryDispatcher, IInterceptableDispatcher
{
    private readonly IQueryDispatcher _inner;
    private readonly NexumTelemetryOptions _options;
    private readonly NexumInstrumentation _instrumentation;
    private readonly IInterceptableDispatcher _interceptable;

    public TracingQueryDispatcher(
        IQueryDispatcher inner,
        NexumTelemetryOptions options,
        NexumInstrumentation instrumentation)
    {
        _inner = inner;
        _options = options;
        _instrumentation = instrumentation;
        _interceptable = inner as IInterceptableDispatcher
            ?? throw new InvalidOperationException(
                $"The inner IQueryDispatcher '{inner.GetType().FullName}' must also implement " +
                $"IInterceptableDispatcher to support Source Generator Tier 3 intercepted dispatch.");
    }

    public async ValueTask<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query, CancellationToken ct = default)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            return await _inner.DispatchAsync(query, ct).ConfigureAwait(false);
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(query.GetType());
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Query {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult? result = await _inner.DispatchAsync(query, ct).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _instrumentation.RecordDispatchMetrics(queryTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _instrumentation.RecordDispatchMetrics(queryTypeName, "failure", startTimestamp);
            throw;
        }
    }

    public async IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IStreamQuery<TResult> query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            await foreach (TResult? item in _inner.StreamAsync(query, ct).ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(query.GetType());
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Stream {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        bool succeeded = false;
        try
        {
            await foreach (TResult? item in _inner.StreamAsync(query, ct).ConfigureAwait(false))
            {
                yield return item;
            }

            succeeded = true;
        }
        finally
        {
            activity?.SetStatus(succeeded ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
    }

    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedCommandAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        throw new NotSupportedException("Use ICommandDispatcher for command interception.");
    }

    async ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedQueryAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            return await _interceptable.DispatchInterceptedQueryAsync(query, compiledPipeline, ct)
                .ConfigureAwait(false);
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(typeof(TQuery));
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Query {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult? result = await _interceptable.DispatchInterceptedQueryAsync(query, compiledPipeline, ct)
                .ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _instrumentation.RecordDispatchMetrics(queryTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _instrumentation.RecordDispatchMetrics(queryTypeName, "failure", startTimestamp);
            throw;
        }
    }

    async IAsyncEnumerable<TResult> IInterceptableDispatcher.StreamInterceptedAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> compiledPipeline,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            await foreach (TResult? item in _interceptable.StreamInterceptedAsync(query, compiledPipeline, ct)
                .ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(typeof(TQuery));
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Stream {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        bool succeeded = false;
        try
        {
            await foreach (TResult? item in _interceptable.StreamInterceptedAsync(query, compiledPipeline, ct)
                .ConfigureAwait(false))
            {
                yield return item;
            }

            succeeded = true;
        }
        finally
        {
            activity?.SetStatus(succeeded ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
        }
    }
}
