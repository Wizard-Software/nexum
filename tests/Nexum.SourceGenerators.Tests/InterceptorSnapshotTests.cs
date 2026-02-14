using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class InterceptorSnapshotTests
{
    [Fact]
    public Task CommandInterceptor_WithHandler_GeneratesInterceptorAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task QueryInterceptor_WithHandler_GeneratesInterceptorAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record GetOrderQuery(Guid Id) : IQuery<string>;

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            public class Consumer
            {
                private readonly IQueryDispatcher _dispatcher;

                public Consumer(IQueryDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<string> GetOrderAsync(Guid id)
                {
                    var query = new GetOrderQuery(id);
                    return await _dispatcher.DispatchAsync(query);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task StreamQueryInterceptor_WithHandler_GeneratesInterceptorAsync()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record StreamOrdersQuery() : IStreamQuery<string>;

            [StreamQueryHandler]
            public sealed class StreamOrdersQueryHandler : IStreamQueryHandler<StreamOrdersQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(StreamOrdersQuery query, CancellationToken ct = default)
                {
                    yield return "order1";
                    yield return "order2";
                }
            }

            public class Consumer
            {
                private readonly IQueryDispatcher _dispatcher;

                public Consumer(IQueryDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public IAsyncEnumerable<string> StreamOrdersAsync()
                {
                    var query = new StreamOrdersQuery();
                    return _dispatcher.StreamAsync(query);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task CommandInterceptor_WithBehaviors_UsesTier2PipelineAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [BehaviorOrder(1)]
            public sealed class LoggingBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task MultipleCallSites_GeneratesNumberedInterceptorsAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            public record UpdateOrderCommand(Guid Id, string Name) : ICommand<Unit>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [CommandHandler]
            public sealed class UpdateOrderCommandHandler : ICommandHandler<UpdateOrderCommand, Unit>
            {
                public ValueTask<Unit> HandleAsync(UpdateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Unit.Value);
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }

                public async ValueTask UpdateOrderAsync(Guid id)
                {
                    var command = new UpdateOrderCommand(id, "updated");
                    await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }
}
