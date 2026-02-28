using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Decorator that adds OpenTelemetry tracing and metrics to command dispatch.
/// Wraps the inner <see cref="ICommandDispatcher"/> with Activity spans and metric recording.
/// Also implements <see cref="IInterceptableDispatcher"/> to support Source Generator Tier 3
/// interceptors while preserving the tracing/metrics layer.
/// </summary>
internal sealed class TracingCommandDispatcher : ICommandDispatcher, IInterceptableDispatcher
{
    private readonly ICommandDispatcher _inner;
    private readonly IInterceptableDispatcher _interceptable;
    private readonly NexumTelemetryOptions _options;
    private readonly NexumInstrumentation _instrumentation;

    public TracingCommandDispatcher(
        ICommandDispatcher inner,
        NexumTelemetryOptions options,
        NexumInstrumentation instrumentation)
    {
        _inner = inner;
        _options = options;
        _instrumentation = instrumentation;
        _interceptable = inner as IInterceptableDispatcher
            ?? throw new InvalidOperationException(
                $"The inner ICommandDispatcher ({inner.GetType().FullName}) must also implement " +
                $"IInterceptableDispatcher to support Source Generator Tier 3 interceptors.");
    }

    public async ValueTask<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command, CancellationToken ct = default)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            return await _inner.DispatchAsync(command, ct).ConfigureAwait(false);
        }

        string commandTypeName = NexumInstrumentation.GetTypeName(command.GetType());
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Command {commandTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.command.type", commandTypeName);
        activity?.SetTag("nexum.command.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult result = await _inner.DispatchAsync(command, ct).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _instrumentation.RecordDispatchMetrics(commandTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _instrumentation.RecordDispatchMetrics(commandTypeName, "failure", startTimestamp);
            throw;
        }
    }

    async ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedCommandAsync<TCommand, TResult>(
        TCommand command,
        Func<TCommand, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct)
    {
        if (!_options.EnableTracing && !_options.EnableMetrics)
        {
            return await _interceptable
                .DispatchInterceptedCommandAsync(command, compiledPipeline, ct)
                .ConfigureAwait(false);
        }

        string commandTypeName = NexumInstrumentation.GetTypeName(typeof(TCommand));
        using Activity? activity = _options.EnableTracing
            ? _instrumentation.ActivitySource.StartActivity(
                $"Nexum.Command {commandTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.command.type", commandTypeName);
        activity?.SetTag("nexum.command.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult result = await _interceptable
                .DispatchInterceptedCommandAsync(command, compiledPipeline, ct)
                .ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            _instrumentation.RecordDispatchMetrics(commandTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _instrumentation.RecordDispatchMetrics(commandTypeName, "failure", startTimestamp);
            throw;
        }
    }

    ValueTask<TResult> IInterceptableDispatcher.DispatchInterceptedQueryAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, ValueTask<TResult>> compiledPipeline,
        CancellationToken ct) =>
        throw new NotSupportedException("Use IQueryDispatcher for query interception.");

    IAsyncEnumerable<TResult> IInterceptableDispatcher.StreamInterceptedAsync<TQuery, TResult>(
        TQuery query,
        Func<TQuery, IServiceProvider, CancellationToken, IAsyncEnumerable<TResult>> compiledPipeline,
        CancellationToken ct) =>
        throw new NotSupportedException("Use IQueryDispatcher for stream interception.");
}
