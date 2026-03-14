using PublicApiGenerator;

namespace Nexum.Contract.Tests;

[Trait("Category", "Contract")]
public sealed class MigrationMediatRApiSurfaceTests
{
    private static readonly ApiGeneratorOptions s_options = new()
    {
        ExcludeAttributes =
        [
            "System.Runtime.CompilerServices.InternalsVisibleToAttribute",
            "System.Runtime.Versioning.TargetFrameworkAttribute",
            "System.Reflection.AssemblyMetadataAttribute",
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
        ],
    };

    [Fact]
    public Task NexumMigrationMediatR_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.Migration.MediatR.MediatRCommandAdapter<,>).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumMigrationMediatR");
    }
}
