using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class MissingHandlerDiagnosticAnalyzerTests
{
    [Fact]
    public async Task CommandWithoutHandler_ReportsNEXUM001Async()
    {
        // Arrange
        var source = """
            using System;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            """;

        var analyzer = new MissingHandlerDiagnosticAnalyzer();

        // Act
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM001");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUM001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("CreateOrderCommand");
        diagnostic.GetMessage().Should().Contain("CommandHandler");
    }

    [Fact]
    public async Task HandlerWithoutAttribute_ReportsNEXUM003Async()
    {
        // Arrange
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        var analyzer = new MissingHandlerDiagnosticAnalyzer();

        // Act
        var diagnostics = await GeneratorTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUM003");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUM003");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("CreateOrderCommandHandler");
        diagnostic.GetMessage().Should().Contain("CommandHandler");
    }
}
