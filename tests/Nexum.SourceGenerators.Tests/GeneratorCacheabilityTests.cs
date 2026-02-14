using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class GeneratorCacheabilityTests
{
    [Fact]
    public void UnchangedInput_ReturnsCached()
    {
        // Arrange
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record PingCommand() : ICommand<string>;

            [CommandHandler]
            public sealed class PingHandler : ICommandHandler<PingCommand, string>
            {
                public ValueTask<string> HandleAsync(PingCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult("pong");
            }
            """;

        var compilation = GeneratorTestHelper.CreateCompilation(source);
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Act - First run
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Act - Second run with same compilation
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Assert
        var result = driver.GetRunResult();
        result.Results[0].TrackedSteps
            .SelectMany(s => s.Value)
            .SelectMany(s => s.Outputs)
            .Should().AllSatisfy(o => o.Reason.Should().BeOneOf(
                IncrementalStepRunReason.Cached,
                IncrementalStepRunReason.Unchanged));
    }

    [Fact]
    public void ModifiedInput_ReturnsModified()
    {
        // Arrange
        var source1 = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record PingCommand() : ICommand<string>;

            [CommandHandler]
            public sealed class PingHandler : ICommandHandler<PingCommand, string>
            {
                public ValueTask<string> HandleAsync(PingCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult("pong");
            }
            """;

        var source2 = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record EchoCommand(string Message) : ICommand<string>;

            [CommandHandler]
            public sealed class EchoHandler : ICommandHandler<EchoCommand, string>
            {
                public ValueTask<string> HandleAsync(EchoCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(command.Message);
            }
            """;

        var compilation1 = GeneratorTestHelper.CreateCompilation(source1);
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Act - First run
        driver = driver.RunGenerators(compilation1, TestContext.Current.CancellationToken);

        // Act - Second run with different source
        var compilation2 = GeneratorTestHelper.CreateCompilation(source2);
        driver = driver.RunGenerators(compilation2, TestContext.Current.CancellationToken);

        // Assert
        var result = driver.GetRunResult();
        result.Results[0].TrackedSteps
            .SelectMany(s => s.Value)
            .SelectMany(s => s.Outputs)
            .Should().Contain(o => o.Reason == IncrementalStepRunReason.Modified);
    }
}
