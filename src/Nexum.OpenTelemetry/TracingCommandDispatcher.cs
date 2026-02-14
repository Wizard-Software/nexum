using System.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.OpenTelemetry;

/// <summary>
/// Decorator that adds OpenTelemetry tracing and metrics to command dispatch.
/// Wraps the inner <see cref="ICommandDispatcher"/> with Activity spans and metric recording.
/// </summary>
internal sealed class TracingCommandDispatcher(
    ICommandDispatcher inner,
    NexumTelemetryOptions options,
    NexumInstrumentation instrumentation) : ICommandDispatcher
{
    public async ValueTask<TResult> DispatchAsync<TResult>(
        ICommand<TResult> command, CancellationToken ct = default)
    {
        if (!options.EnableTracing && !options.EnableMetrics)
        {
            return await inner.DispatchAsync(command, ct).ConfigureAwait(false);
        }

        string commandTypeName = NexumInstrumentation.GetTypeName(command.GetType());
        using Activity? activity = options.EnableTracing
            ? instrumentation.ActivitySource.StartActivity(
                $"Nexum.Command {commandTypeName}", ActivityKind.Internal)
            : null;

        activity?.SetTag("nexum.command.type", commandTypeName);
        activity?.SetTag("nexum.command.result_type", NexumInstrumentation.GetTypeName(typeof(TResult)));

        long startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            TResult? result = await inner.DispatchAsync(command, ct).ConfigureAwait(false);
            activity?.SetStatus(ActivityStatusCode.Ok);
            instrumentation.RecordDispatchMetrics(commandTypeName, "success", startTimestamp);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            instrumentation.RecordDispatchMetrics(commandTypeName, "failure", startTimestamp);
            throw;
        }
    }
}
