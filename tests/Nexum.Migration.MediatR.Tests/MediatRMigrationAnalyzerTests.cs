using Microsoft.CodeAnalysis;
using Nexum.Migration.MediatR.Analyzers;
using Nexum.Migration.MediatR.Tests.Helpers;

namespace Nexum.Migration.MediatR.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class MediatRMigrationAnalyzerTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // NEXUMM001 — MediatR IRequest<T> without Nexum ICommand<T> or IQuery<T>
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NEXUMM001_IRequestWithoutNexumInterface_ReportsDiagnosticAsync()
    {
        // Arrange
        var source = """
            using System;
            using MediatR;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : IRequest<Guid>;
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUMM001");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUMM001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage().Should().Contain("CreateOrderCommand");
        diagnostic.GetMessage().Should().Contain("Guid");
    }

    [Fact]
    public async Task NEXUMM001_IRequestWithICommand_NoDiagnosticAsync()
    {
        // Arrange — implements both MediatR IRequest<T> and Nexum ICommand<T> → no NEXUMM001
        var source = """
            using System;
            using MediatR;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : IRequest<Guid>, ICommand<Guid>;
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().NotContain(d => d.Id == "NEXUMM001");
    }

    [Fact]
    public async Task NEXUMM001_IRequestWithIQuery_NoDiagnosticAsync()
    {
        // Arrange — implements both MediatR IRequest<T> and Nexum IQuery<T> → no NEXUMM001
        var source = """
            using System.Collections.Generic;
            using MediatR;
            using Nexum.Abstractions;

            namespace TestApp;

            public record GetOrdersQuery(int Page) : IRequest<List<string>>, IQuery<List<string>>;
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().NotContain(d => d.Id == "NEXUMM001");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // NEXUMM002 — MediatR IRequestHandler<,> without Nexum handler
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NEXUMM002_IRequestHandlerWithoutNexumHandler_ReportsDiagnosticAsync()
    {
        // Arrange
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using MediatR;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : IRequest<Guid>;

            public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Guid>
            {
                public Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
                    => Task.FromResult(Guid.NewGuid());
            }
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        var nexumm002 = diagnostics.Where(d => d.Id == "NEXUMM002").ToList();
        nexumm002.Should().ContainSingle();

        var diagnostic = nexumm002[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage().Should().Contain("CreateOrderCommandHandler");
        diagnostic.GetMessage().Should().Contain("CreateOrderCommand");
        diagnostic.GetMessage().Should().Contain("Guid");
    }

    [Fact]
    public async Task NEXUMM002_IRequestHandlerWithNexumHandler_NoDiagnosticAsync()
    {
        // Arrange — handler implements both MediatR and Nexum handler interfaces
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using MediatR;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : IRequest<Guid>, ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler
                : IRequestHandler<CreateOrderCommand, Guid>,
                  ICommandHandler<CreateOrderCommand, Guid>
            {
                public Task<Guid> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
                    => Task.FromResult(Guid.NewGuid());

                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().NotContain(d => d.Id == "NEXUMM002");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // NEXUMM003 — MediatR INotification without Nexum INotification
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NEXUMM003_MediatRNotificationWithoutNexum_ReportsDiagnosticAsync()
    {
        // Arrange
        var source = """
            using MediatR;

            namespace TestApp;

            public record OrderCreatedEvent(string OrderId) : INotification;
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().ContainSingle(d => d.Id == "NEXUMM003");

        var diagnostic = diagnostics.Single(d => d.Id == "NEXUMM003");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Info);
        diagnostic.GetMessage().Should().Contain("OrderCreatedEvent");
    }

    [Fact]
    public async Task NEXUMM003_MediatRNotificationWithNexum_NoDiagnosticAsync()
    {
        // Arrange — implements both MediatR and Nexum INotification
        var source = """
            using MediatR;
            using NexumNotification = Nexum.Abstractions.INotification;

            namespace TestApp;

            public record OrderCreatedEvent(string OrderId) : INotification, NexumNotification;
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().NotContain(d => d.Id == "NEXUMM003");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // No MediatR types
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NoMediatRTypes_NoDiagnosticsAsync()
    {
        // Arrange — pure Nexum types, no MediatR
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
            """;

        var analyzer = new MediatRMigrationAnalyzer();

        // Act
        var diagnostics = await MigrationAnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(source, analyzer);

        // Assert
        diagnostics.Should().NotContain(d => d.Id.StartsWith("NEXUMM", StringComparison.Ordinal));
    }
}
