using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class NexumHandlerRegistryGeneratorSnapshotTests
{
    [Fact]
    public Task SingleCommandHandler_GeneratesRegistryAsync()
    {
        var source = """
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
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task MultipleHandlerTypes_GeneratesFullRegistryAsync()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            public record GetOrderQuery(Guid Id) : IQuery<string>;
            public record StreamOrdersQuery() : IStreamQuery<string>;
            public record OrderCreatedNotification(Guid Id) : INotification;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            [StreamQueryHandler]
            public sealed class StreamOrdersQueryHandler : IStreamQueryHandler<StreamOrdersQuery, string>
            {
                public IAsyncEnumerable<string> HandleAsync(StreamOrdersQuery query, CancellationToken ct = default)
                    => throw new NotImplementedException();
            }

            [NotificationHandler]
            public sealed class OrderCreatedNotificationHandler : INotificationHandler<OrderCreatedNotification>
            {
                public ValueTask HandleAsync(OrderCreatedNotification notification, CancellationToken ct = default)
                    => ValueTask.CompletedTask;
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HandlerWithSingletonLifetime_GeneratesCorrectLifetimeAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record PingCommand() : ICommand<string>;

            [CommandHandler]
            [HandlerLifetime(NexumLifetime.Singleton)]
            public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
            {
                public ValueTask<string> HandleAsync(PingCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult("pong");
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public void NoHandlers_GeneratesNoSource()
    {
        var source = """
            using Nexum.Abstractions;

            namespace TestApp;

            public record SomeCommand() : ICommand<string>;
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        GeneratorDriverRunResult result = driver.GetRunResult();
        result.Results[0].GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public Task VoidCommandHandler_GeneratesCorrectRegistrationAsync()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record DeleteOrderCommand(Guid Id) : IVoidCommand;

            [CommandHandler]
            public sealed class DeleteOrderCommandHandler : ICommandHandler<DeleteOrderCommand, Unit>
            {
                public ValueTask<Unit> HandleAsync(DeleteOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Unit.Value);
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task MultipleNotificationHandlers_AllRegisteredAsync()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record OrderCreatedEvent() : INotification;

            [NotificationHandler]
            public sealed class LoggingHandler : INotificationHandler<OrderCreatedEvent>
            {
                public ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct = default)
                    => ValueTask.CompletedTask;
            }

            [NotificationHandler]
            public sealed class EmailHandler : INotificationHandler<OrderCreatedEvent>
            {
                public ValueTask HandleAsync(OrderCreatedEvent notification, CancellationToken ct = default)
                    => ValueTask.CompletedTask;
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task RecordHandler_GeneratesCorrectRegistrationAsync()
    {
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record GetItemQuery(int Id) : IQuery<string>;

            [QueryHandler]
            public sealed record class GetItemQueryHandler : IQueryHandler<GetItemQuery, string>
            {
                public ValueTask<string> HandleAsync(GetItemQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("item");
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HandlersWithBehaviors_GeneratesPipelineRegistryAsync()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            public record GetOrderQuery(Guid Id) : IQuery<string>;
            public record StreamOrdersQuery() : IStreamQuery<string>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            [StreamQueryHandler]
            public sealed class StreamOrdersQueryHandler : IStreamQueryHandler<StreamOrdersQuery, string>
            {
                public IAsyncEnumerable<string> HandleAsync(StreamOrdersQuery query, CancellationToken ct = default)
                    => throw new NotImplementedException();
            }

            [BehaviorOrder(1)]
            public sealed class LoggingBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }

            [BehaviorOrder(2)]
            public sealed class ValidationBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }

            [BehaviorOrder(1)]
            public sealed class QueryLoggingBehavior<TQuery, TResult> : IQueryBehavior<TQuery, TResult>
                where TQuery : IQuery<TResult>
            {
                public ValueTask<TResult> HandleAsync(TQuery query, QueryHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HandlersWithNoBehaviors_DoesNotGeneratePipelineRegistryAsync()
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

            public sealed class LoggingBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HandlersWithSingleBehavior_GeneratesPipelineRegistryAsync()
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
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HandlersWithMixedBehaviors_GeneratesPipelineRegistryAsync()
    {
        var source = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            public record GetOrderQuery(Guid Id) : IQuery<string>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            [BehaviorOrder(1)]
            public sealed class LoggingBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }

            public sealed class RuntimeOnlyBehavior<TCommand, TResult> : ICommandBehavior<TCommand, TResult>
                where TCommand : ICommand<TResult>
            {
                public ValueTask<TResult> HandleAsync(TCommand command, CommandHandlerDelegate<TResult> next, CancellationToken ct)
                    => next(ct);
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }
}
