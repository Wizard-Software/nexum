using System.Diagnostics;
using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Decorator that adds OpenTelemetry tracing and metrics to query dispatch and streaming.
/// Wraps the inner <see cref="IQueryDispatcher"/> with Activity spans and metric recording.
/// </summary>
internal sealed class TracingQueryDispatcher(
    IQueryDispatcher inner,
    NexumTelemetryOptions options,
    NexumInstrumentation instrumentation) : IQueryDispatcher
{
    public async ValueTask<TResult> DispatchAsync<TResult>(
        IQuery<TResult> query, CancellationToken ct = default)
    {
        if (!options.EnableTracing && !options.EnableMetrics)
        {
            return await inner.DispatchAsync(query, ct).ConfigureAwait(false);
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(query.GetType());
        using Activity? activity = options.EnableTracing
            ? instrumentation.ActivitySource.StartActivity(
                $"Nexum.Query {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult? result = await inner.DispatchAsync(query, ct).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            instrumentation.RecordDispatchMetrics(queryTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            instrumentation.RecordDispatchMetrics(queryTypeName, "failure", startTimestamp);
            throw;
        }
    }

    public async IAsyncEnumerable<TResult> StreamAsync<TResult>(
        IStreamQuery<TResult> query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!options.EnableTracing && !options.EnableMetrics)
        {
            await foreach (TResult? item in inner.StreamAsync(query, ct).ConfigureAwait(false))
            {
                yield return item;
            }

            yield break;
        }

        string queryTypeName = NexumInstrumentation.GetTypeName(query.GetType());
        using Activity? activity = options.EnableTracing
            ? instrumentation.ActivitySource.StartActivity(
                $"Nexum.Stream {queryTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.query.type", queryTypeName);
        activity?.SetTag("nexum.query.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        bool succeeded = false;
        try
        {
            await foreach (TResult? item in inner.StreamAsync(query, ct).ConfigureAwait(false))
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
