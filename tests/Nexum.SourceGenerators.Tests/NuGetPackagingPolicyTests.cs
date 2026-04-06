using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Nexum.SourceGenerators.Tests.Helpers;

namespace Nexum.SourceGenerators.Tests;

/// <summary>
/// Guards against NuGet packaging configurations that cause the source generator
/// to be registered multiple times in a consumer's compilation.
/// See: GitHub issue #12 — double delivery via analyzers/ + buildTransitive/.targets.
/// </summary>
[Trait("Category", "SourceGenerator")]
public sealed class NuGetPackagingPolicyTests
{
    private static readonly string s_solutionRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void DoubleRegistration_ProducesCompilationErrors()
    {
        // Arrange — a simple handler project
        var source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(source, "TestApp");

        // Act — run the generator and collect generated sources
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Simulate double-delivery: add generated sources to compilation TWICE
        CSharpCompilation updatedCompilation = compilation;
        foreach (GeneratorRunResult generatorResult in runResult.Results)
        {
            foreach (GeneratedSourceResult generated in generatorResult.GeneratedSources)
            {
                updatedCompilation = updatedCompilation.AddSyntaxTrees(generated.SyntaxTree);
            }
        }

        // Add the same sources again (simulating the second analyzer registration)
        foreach (GeneratorRunResult generatorResult in runResult.Results)
        {
            foreach (GeneratedSourceResult generated in generatorResult.GeneratedSources)
            {
                // Parse fresh to avoid same SyntaxTree reference deduplication
                SyntaxTree duplicate = CSharpSyntaxTree.ParseText(
                    generated.SyntaxTree.GetText(TestContext.Current.CancellationToken),
                    path: generated.HintName + ".duplicate",
                    cancellationToken: TestContext.Current.CancellationToken);
                updatedCompilation = updatedCompilation.AddSyntaxTrees(duplicate);
            }
        }

        // Assert — compilation must have errors (CS0101: duplicate type definition)
        Diagnostic[] errors = updatedCompilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        errors.Should().NotBeEmpty("double-registering the source generator must produce compilation errors");
        errors.Should().Contain(d => d.Id == "CS0101",
            "duplicate NexumHandlerRegistry type definition expected");
    }

    [Fact]
    public void DI_package_must_not_double_deliver_analyzer()
    {
        // Arrange
        string targetsPath = Path.Combine(
            s_solutionRoot, "src", "Nexum.Extensions.DependencyInjection",
            "buildTransitive", "Nexum.Extensions.DependencyInjection.targets");

        // Act & Assert — the .targets file must not exist or must not register <Analyzer> items.
        // NuGet's analyzers/dotnet/cs/ convention already delivers the generator DLL.
        // A .targets file that also registers <Analyzer> causes double-delivery (GitHub #12).
        if (File.Exists(targetsPath))
        {
            string content = File.ReadAllText(targetsPath);
            content.Should().NotContain("<Analyzer",
                "buildTransitive .targets must not register analyzers — " +
                "the analyzers/dotnet/cs/ NuGet convention already delivers them. " +
                "Double delivery causes CS0101/CS0579 in consumer projects (GitHub #12).");
        }

        // Also verify the csproj does not pack .targets files with analyzer registrations
        string csprojPath = Path.Combine(
            s_solutionRoot, "src", "Nexum.Extensions.DependencyInjection",
            "Nexum.Extensions.DependencyInjection.csproj");

        File.Exists(csprojPath).Should().BeTrue("DI extension csproj must exist");
        string csproj = File.ReadAllText(csprojPath);

        // If the csproj packs analyzer DLLs into analyzers/dotnet/cs/,
        // it must NOT also pack .targets files into buildTransitive/ or build/
        bool packsAnalyzerDll = csproj.Contains("PackagePath=\"analyzers/dotnet/cs\"");
        if (packsAnalyzerDll)
        {
            // Ensure no .targets are packed that could register the same analyzer
            csproj.Should().NotContain("PackagePath=\"buildTransitive/\"",
                "csproj must not pack .targets into buildTransitive/ when analyzer DLL " +
                "is already in analyzers/dotnet/cs/ — this causes double delivery (GitHub #12)");
            csproj.Should().NotContain("PackagePath=\"build/\"",
                "csproj must not pack .targets into build/ when analyzer DLL " +
                "is already in analyzers/dotnet/cs/ — this causes double delivery (GitHub #12)");
        }
    }
}
