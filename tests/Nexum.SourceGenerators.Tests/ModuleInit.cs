using System.Runtime.CompilerServices;

namespace Nexum.SourceGenerators.Tests;

internal static class ModuleInit
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        VerifySourceGenerators.Initialize();
    }
}
