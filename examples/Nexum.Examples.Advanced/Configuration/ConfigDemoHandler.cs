using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Configuration;

public sealed class ConfigDemoHandler(NexumOptions options) : ICommandHandler<ConfigDemoCommand, string>
{
    public ValueTask<string> HandleAsync(ConfigDemoCommand command, CancellationToken ct = default)
    {
        var summary = $"DefaultPublishStrategy={options.DefaultPublishStrategy}, " +
                      $"FireAndForgetTimeout={options.FireAndForgetTimeout.TotalSeconds}s, " +
                      $"MaxDispatchDepth={options.MaxDispatchDepth}";
        return ValueTask.FromResult(summary);
    }
}
