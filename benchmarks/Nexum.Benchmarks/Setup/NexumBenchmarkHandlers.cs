#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Nexum.Abstractions;

namespace Nexum.Benchmarks.Setup;

// Nexum handlers (no-op, constant returns)
public sealed class BenchCommandHandler : ICommandHandler<BenchCommand, Guid>
{
    private static readonly Guid s_fixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    public ValueTask<Guid> HandleAsync(BenchCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(s_fixedGuid);
}

public sealed class BenchQueryHandler : IQueryHandler<BenchQuery, string>
{
    public ValueTask<string> HandleAsync(BenchQuery query, CancellationToken ct = default)
        => ValueTask.FromResult("result");
}

public sealed class BenchStreamQueryHandler : IStreamQueryHandler<BenchStreamQuery, int>
{
    public async IAsyncEnumerable<int> HandleAsync(
        BenchStreamQuery query,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < query.Count; i++)
        {
            yield return i;
        }

        await Task.CompletedTask.ConfigureAwait(false); // Satisfy async requirement
    }
}

public sealed class BenchNotificationHandler1 : INotificationHandler<BenchNotification>
{
    public ValueTask HandleAsync(BenchNotification notification, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}

public sealed class BenchNotificationHandler2 : INotificationHandler<BenchNotification>
{
    public ValueTask HandleAsync(BenchNotification notification, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}

public sealed class BenchNotificationHandler3 : INotificationHandler<BenchNotification>
{
    public ValueTask HandleAsync(BenchNotification notification, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}

// Additional handlers for MediatR comparison (5 handlers)
public sealed class BenchNotificationHandler4 : INotificationHandler<BenchNotification>
{
    public ValueTask HandleAsync(BenchNotification notification, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}

public sealed class BenchNotificationHandler5 : INotificationHandler<BenchNotification>
{
    public ValueTask HandleAsync(BenchNotification notification, CancellationToken ct = default)
        => ValueTask.CompletedTask;
}

// MediatR types
public sealed record MediatRBenchCommand(string Name) : MediatR.IRequest<Guid>;

public sealed record MediatRBenchNotification(string Message) : MediatR.INotification;

// MediatR handler equivalents
public sealed class MediatRBenchCommandHandler : MediatR.IRequestHandler<MediatRBenchCommand, Guid>
{
    private static readonly Guid s_fixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    public Task<Guid> Handle(MediatRBenchCommand request, CancellationToken cancellationToken)
        => Task.FromResult(s_fixedGuid);
}

public sealed class MediatRBenchNotificationHandler1 : MediatR.INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class MediatRBenchNotificationHandler2 : MediatR.INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class MediatRBenchNotificationHandler3 : MediatR.INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class MediatRBenchNotificationHandler4 : MediatR.INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class MediatRBenchNotificationHandler5 : MediatR.INotificationHandler<MediatRBenchNotification>
{
    public Task Handle(MediatRBenchNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
