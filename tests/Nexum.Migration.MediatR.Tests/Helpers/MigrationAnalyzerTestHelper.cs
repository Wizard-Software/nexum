using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Nexum.Abstractions;

namespace Nexum.Migration.MediatR.Tests.Helpers;

internal static class MigrationAnalyzerTestHelper
{
    private static readonly MetadataReference[] s_references = BuildMetadataReferences();

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        params DiagnosticAnalyzer[] analyzers)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create(analyzers));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            s_references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference[] BuildMetadataReferences()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        void AddAssembly(System.Reflection.Assembly assembly)
        {
            string location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && seen.Add(location))
            {
                references.Add(MetadataReference.CreateFromFile(location));
            }
        }

        // Core runtime assemblies
        AddAssembly(typeof(object).Assembly);                    // System.Runtime
        AddAssembly(typeof(Attribute).Assembly);                 // System.Runtime (attributes)
        AddAssembly(typeof(ValueTask<>).Assembly);               // System.Threading.Tasks.Extensions or System.Runtime
        AddAssembly(typeof(CancellationToken).Assembly);         // System.Threading
        AddAssembly(typeof(IAsyncEnumerable<>).Assembly);        // System.Runtime

        // Nexum.Abstractions — provides ICommand<T>, IQuery<T>, INotification, etc.
        AddAssembly(typeof(ICommand).Assembly);

        // MediatR.Contracts — provides IRequest<T>, INotification (core contracts package)
        // MediatR.IRequest<T> lives in MediatR.Contracts, not in MediatR itself.
        // Use global:: prefix to disambiguate from Nexum.Migration.MediatR namespace.
        AddAssembly(typeof(global::MediatR.IRequest<>).Assembly);

        // MediatR — provides IRequestHandler<,> and mediator infrastructure
        AddAssembly(typeof(global::MediatR.IRequestHandler<,>).Assembly);

        // Add System.Runtime explicitly for .NET 10
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
}
