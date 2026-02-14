#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Nexum.Abstractions;

namespace Nexum.Benchmarks.Setup;

// 3 pass-through Nexum command behaviors
public sealed class BenchCommandBehavior1 : ICommandBehavior<BenchCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(
        BenchCommand command,
        CommandHandlerDelegate<Guid> next,
        CancellationToken ct = default)
        => next(ct);
}

public sealed class BenchCommandBehavior2 : ICommandBehavior<BenchCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(
        BenchCommand command,
        CommandHandlerDelegate<Guid> next,
        CancellationToken ct = default)
        => next(ct);
}

public sealed class BenchCommandBehavior3 : ICommandBehavior<BenchCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(
        BenchCommand command,
        CommandHandlerDelegate<Guid> next,
        CancellationToken ct = default)
        => next(ct);
}

// 3 pass-through MediatR pipeline behaviors
public sealed class MediatRBenchBehavior1 : MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>
{
    public Task<Guid> Handle(
        MediatRBenchCommand request,
        MediatR.RequestHandlerDelegate<Guid> next,
        CancellationToken cancellationToken)
        => next();
}

public sealed class MediatRBenchBehavior2 : MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>
{
    public Task<Guid> Handle(
        MediatRBenchCommand request,
        MediatR.RequestHandlerDelegate<Guid> next,
        CancellationToken cancellationToken)
        => next();
}

public sealed class MediatRBenchBehavior3 : MediatR.IPipelineBehavior<MediatRBenchCommand, Guid>
{
    public Task<Guid> Handle(
        MediatRBenchCommand request,
        MediatR.RequestHandlerDelegate<Guid> next,
        CancellationToken cancellationToken)
        => next();
}
