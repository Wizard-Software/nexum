using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class NexumHandlerRegistryGeneratorDiagnosticsTests
{
    [Fact]
    public void DuplicateCommandHandler_ReportsNEXUM002()
    {
        // Arrange
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class Handler1 : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [CommandHandler]
            public sealed class Handler2 : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        // Assert
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM002");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUM002");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("CreateOrderCommand");
        diagnostic.GetMessage().Should().Contain("Handler1");
        diagnostic.GetMessage().Should().Contain("Handler2");
    }

    [Fact]
    public void AttributeWithoutInterface_ReportsNEXUM004()
    {
        // Arrange
        var source = """
            using Nexum.Abstractions;

            namespace TestApp;

            [CommandHandler]
            public sealed class NotAHandler
            {
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        // Act
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var result = driver.GetRunResult();

        // Assert
        var diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM004");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUM004");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("NotAHandler");
        diagnostic.GetMessage().Should().Contain("CommandHandler");
    }
}
