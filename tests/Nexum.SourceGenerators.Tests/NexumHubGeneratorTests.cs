using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class NexumHubGeneratorTests
{
    [Fact]
    public Task HubWithStreamQueryHandler_GeneratesMethodAsync()
    {
        string source = """
            using Nexum.Abstractions;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestApp;

            public sealed record GetOrdersQuery(string CustomerId) : IStreamQuery<string>;

            [StreamQueryHandler]
            public sealed class GetOrdersHandler : IStreamQueryHandler<GetOrdersQuery, string>
            {
                public IAsyncEnumerable<string> HandleAsync(GetOrdersQuery query, CancellationToken ct = default)
                    => throw new System.NotImplementedException();
            }

            [NexumStreamHub]
            public partial class OrderStreamHub : Nexum.Streaming.NexumStreamHubBase
            {
                public OrderStreamHub(
                    Nexum.Abstractions.IQueryDispatcher qd,
                    Nexum.Abstractions.IStreamNotificationPublisher snp) : base(qd, snp) { }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateSignalRCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HubWithStreamNotificationHandler_GeneratesMethodAsync()
    {
        string source = """
            using Nexum.Abstractions;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestApp;

            public sealed record OrderUpdatedNotification(string OrderId) : IStreamNotification<string>;

            [StreamNotificationHandler]
            public sealed class OrderUpdatedHandler : IStreamNotificationHandler<OrderUpdatedNotification, string>
            {
                public IAsyncEnumerable<string> HandleAsync(OrderUpdatedNotification notification, CancellationToken ct = default)
                    => throw new System.NotImplementedException();
            }

            [NexumStreamHub]
            public partial class OrderStreamHub : Nexum.Streaming.NexumStreamHubBase
            {
                public OrderStreamHub(
                    Nexum.Abstractions.IQueryDispatcher qd,
                    Nexum.Abstractions.IStreamNotificationPublisher snp) : base(qd, snp) { }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateSignalRCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        return Verify(driver);
    }

    [Fact]
    public Task HubWithoutAspNetCore_SilentSkipAsync()
    {
        // Use a compilation WITHOUT ASP.NET Core references (no SignalR.Hub type available).
        // The generator must silently skip hub generation — no NexumHub_*.g.cs should be emitted.
        string source = """
            using Nexum.Abstractions;
            using System.Collections.Generic;
            using System.Threading;

            namespace TestApp;

            public sealed record GetOrdersQuery(string CustomerId) : IStreamQuery<string>;

            [StreamQueryHandler]
            public sealed class GetOrdersHandler : IStreamQueryHandler<GetOrdersQuery, string>
            {
                public IAsyncEnumerable<string> HandleAsync(GetOrdersQuery query, CancellationToken ct = default)
                    => throw new System.NotImplementedException();
            }
            """;

        // CreateCompilation excludes ASP.NET Core — Hub type not available, generator must skip silently.
        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Assert — no hub source file generated
        runResult.GeneratedTrees
            .Should().NotContain(t => t.FilePath.Contains("NexumHub_"));

        return Task.CompletedTask;
    }
}
