using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.SourceGenerators.Tests.Helpers;

internal static class GeneratorTestHelper
{
    private static readonly MetadataReference[] s_references = GetMetadataReferences();
    private static readonly MetadataReference[] s_aspNetCoreReferences = GetAspNetCoreMetadataReferences();

    public static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static CSharpCompilation CreateAspNetCoreCompilation(string source, string assemblyName = "TestAssembly")
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            s_aspNetCoreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static GeneratorDriver CreateDriver(CSharpCompilation compilation)
    {
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        return driver.RunGenerators(compilation);
    }

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        // Also run generator first to populate context (generator may add sources)
        var generator = new NexumHandlerRegistryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGenerators(compilation);
        GeneratorDriverRunResult runResult = driver.GetRunResult();

        // Create a new compilation with generated sources added
        CSharpCompilation updatedCompilation = compilation;
        foreach (GeneratorRunResult generatorResult in runResult.Results)
        {
            foreach (GeneratedSourceResult generatedSource in generatorResult.GeneratedSources)
            {
                updatedCompilation = updatedCompilation.AddSyntaxTrees(generatedSource.SyntaxTree);
            }
        }

        CompilationWithAnalyzers compilationWithAnalyzers = updatedCompilation.WithAnalyzers(
            ImmutableArray.Create(analyzers));
        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        // Core runtime assemblies
        var assemblies = new[]
        {
            typeof(object).Assembly,                    // System.Runtime
            typeof(Attribute).Assembly,                 // System.Runtime (attributes)
            typeof(ValueTask<>).Assembly,               // System.Threading.Tasks.Extensions or System.Runtime
            typeof(ICommand).Assembly,                  // Nexum.Abstractions
            typeof(CancellationToken).Assembly,         // System.Threading
            typeof(IAsyncEnumerable<>).Assembly,        // System.Runtime
        };

        var references = new List<MetadataReference>();
        var seen = new HashSet<string>();

        foreach (var assembly in assemblies)
        {
            if (seen.Add(assembly.Location))
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
        }

        // Add System.Runtime reference explicitly for .NET 10
        string runtimeDir = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
        string systemRuntimePath = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(systemRuntimePath) && seen.Add(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add netstandard.dll for compatibility
        string netstandardPath = Path.Combine(runtimeDir, "netstandard.dll");
        if (File.Exists(netstandardPath) && seen.Add(netstandardPath))
        {
            references.Add(MetadataReference.CreateFromFile(netstandardPath));
        }

        // Add System.Collections for IEnumerable
        string collectionsPath = Path.Combine(runtimeDir, "System.Collections.dll");
        if (File.Exists(collectionsPath) && seen.Add(collectionsPath))
        {
            references.Add(MetadataReference.CreateFromFile(collectionsPath));
        }

        return references.ToArray();
    }

    private static MetadataReference[] GetAspNetCoreMetadataReferences()
    {
        // Start with base references
        List<MetadataReference> references = [.. s_references];
        HashSet<string> seen = [];
        foreach (MetadataReference metaRef in s_references)
        {
            if (metaRef is PortableExecutableReference peRef && peRef.FilePath is not null)
            {
                seen.Add(peRef.FilePath);
            }
        }

        // Add ASP.NET Core assemblies from the loaded framework
        Type[] aspNetCoreTypes =
        [
            typeof(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder),
            typeof(Microsoft.AspNetCore.Http.HttpContext),
            typeof(Microsoft.AspNetCore.Http.TypedResults),
            typeof(Microsoft.AspNetCore.Mvc.ProblemDetails),
        ];

        foreach (Type type in aspNetCoreTypes)
        {
            string location = type.Assembly.Location;
            if (!string.IsNullOrEmpty(location) && seen.Add(location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        return references.ToArray();
    }
}
