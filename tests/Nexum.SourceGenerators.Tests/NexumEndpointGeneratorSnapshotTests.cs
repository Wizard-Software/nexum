using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class NexumEndpointGeneratorSnapshotTests
{
    [Fact]
    public Task SimpleCommandEndpoint_GeneratesMapNexumEndpointsAsync()
    {
        string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Post, "/api/orders")]
            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task VoidCommandEndpoint_GeneratesNoContentAsync()
    {
        string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Delete, "/api/orders/{id}")]
            public record DeleteOrderCommand(Guid Id) : IVoidCommand;

            [CommandHandler]
            public sealed class DeleteOrderCommandHandler : ICommandHandler<DeleteOrderCommand, Unit>
            {
                public ValueTask<Unit> HandleAsync(DeleteOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Unit.Value);
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task QueryEndpoint_GeneratesGetWithAsParametersAsync()
    {
        string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Get, "/api/orders/{id}")]
            public record GetOrderQuery(Guid Id) : IQuery<string>;

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task EndpointWithNameAndGroup_UsesProvidedNameAndTagsAsync()
    {
        string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Post, "/api/orders", Name = "CreateNewOrder", GroupName = "Orders")]
            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }
}
