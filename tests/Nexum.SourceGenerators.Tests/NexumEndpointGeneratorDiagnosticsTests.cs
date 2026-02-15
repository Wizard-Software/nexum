using System.Collections.Immutable;
using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class NexumEndpointGeneratorDiagnosticsTests
{
    [Fact]
    public void EndpointOnNonMessageType_ReportsNEXUM008()
    {
        string source = """
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Post, "/api/things")]
            public sealed class NotACommand
            {
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source);
        NexumHandlerRegistryGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Check compilation diagnostics first
        ImmutableArray<Diagnostic> compilationDiagnostics = compilation.GetDiagnostics(TestContext.Current.CancellationToken);
        compilationDiagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error, "Compilation should not have errors");

        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM008");
    }

    [Fact]
    public void DuplicateEndpointRoute_ReportsNEXUM009()
    {
        string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            [NexumEndpoint(NexumHttpMethod.Post, "/api/orders")]
            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [NexumEndpoint(NexumHttpMethod.Post, "/api/orders")]
            public record CreateOrderCommand2(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class Handler1 : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [CommandHandler]
            public sealed class Handler2 : ICommandHandler<CreateOrderCommand2, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand2 command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateAspNetCoreCompilation(source);
        NexumHandlerRegistryGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult result = driver.GetRunResult();

        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM009");
    }

    [Fact]
    public void EndpointWithoutAspNetCore_SilentlySkips()
    {
        // Use CreateCompilation (without ASP.NET Core) — endpoint should be silently skipped
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

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source);
        NexumHandlerRegistryGenerator generator = new();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Should NOT generate NexumEndpointRegistration.g.cs
        result.Results[0].GeneratedSources.Should().NotContain(s => s.HintName == "NexumEndpointRegistration.g.cs");

        // Should NOT report any endpoint-related diagnostics
        result.Results[0].Diagnostics.Should().NotContain(d => d.Id == "NEXUM008" || d.Id == "NEXUM009");
    }
}
