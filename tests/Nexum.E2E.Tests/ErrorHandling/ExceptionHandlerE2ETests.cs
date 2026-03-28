using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.ErrorHandling;

[Trait("Category", "E2E")]
public sealed class ExceptionHandlerE2ETests : IDisposable
{
    private readonly Microsoft.Extensions.Hosting.IHost _host;

    public ExceptionHandlerE2ETests()
    {
        var exceptionLog = new List<string>();

        _host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddSingleton(exceptionLog);
            services.AddNexumExceptionHandler<FailingCommandExceptionHandler>();
        });
    }

    public void Dispose() => _host.Dispose();

    // E2E-030: FailingCommand triggers exception handler (side-effect logged) AND re-throws to caller
    [Fact]
    public async Task DispatchAsync_FailingCommandWithExceptionHandler_LogsAndRethrows()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();
        var exceptionLog = _host.Services.GetRequiredService<List<string>>();

        var act = async () => await dispatcher.DispatchAsync(new FailingCommand("test error"), ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("test error");

        exceptionLog.Should().ContainSingle()
            .Which.Should().Be("ExceptionHandler:test error");
    }

    // E2E-013: Behavior that throws before calling next — exception propagates to caller
    [Fact]
    public async Task DispatchAsync_BehaviorThrowsBeforeNext_ExceptionPropagates()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            // TrackingCommandBehavior (auto-discovered) requires List<string> — register a dummy log.
            services.AddSingleton(new List<string>());
            services.AddSingleton<ICommandBehavior<TrackedCommand, string>, ThrowingCommandBehavior>();
        });

        var dispatcher = host.Services.GetRequiredService<ICommandDispatcher>();

        var act = async () => await dispatcher.DispatchAsync(new TrackedCommand("input"), ct);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Behavior threw before next");
    }

    // E2E-034: Stream handler yields 5 items then throws — caller receives 5 items then exception on 6th iteration
    [Fact]
    public async Task StreamAsync_HandlerThrowsDuringEnumeration_YieldsItemsThenThrows()
    {
        var ct = TestContext.Current.CancellationToken;

        using var host = NexumTestHost.CreateHost(configureServices: services =>
        {
            services.AddNexumHandler<IStreamQueryHandler<FaultingStreamQuery, int>, FaultingStreamQueryHandler>();
        });

        var dispatcher = host.Services.GetRequiredService<IQueryDispatcher>();
        var received = new List<int>();
        Exception? caughtException = null;

        try
        {
            await foreach (var item in dispatcher.StreamAsync(new FaultingStreamQuery(), ct))
            {
                received.Add(item);
            }
        }
        catch (InvalidOperationException ex)
        {
            caughtException = ex;
        }

        received.Should().HaveCount(5).And.Equal(1, 2, 3, 4, 5);
        caughtException.Should().NotBeNull()
            .And.BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Stream failed after 5 items");
    }
}

// Inline behavior for E2E-013: throws before calling next
file sealed class ThrowingCommandBehavior : ICommandBehavior<TrackedCommand, string>
{
    public ValueTask<string> HandleAsync(
        TrackedCommand command,
        CommandHandlerDelegate<string> next,
        CancellationToken ct = default)
    {
        throw new InvalidOperationException("Behavior threw before next");
    }
}

// Inline stream query and handler for E2E-034
file sealed record FaultingStreamQuery : IStreamQuery<int>;

file sealed class FaultingStreamQueryHandler : IStreamQueryHandler<FaultingStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        FaultingStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= 5; i++)
        {
            yield return i;
            await Task.Yield();
        }

        throw new InvalidOperationException("Stream failed after 5 items");
    }
}
