using PublicApiGenerator;

namespace Nexum.Contract.Tests;

[Trait("Category", "Contract")]
public sealed class PublicApiTests
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
    public Task NexumAbstractions_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.Abstractions.ICommand).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumAbstractions");
    }

    [Fact]
    public Task Nexum_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.CommandDispatcher).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("Nexum");
    }

    [Fact]
    public Task NexumResults_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.Results.Result<,>).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumResults");
    }

    [Fact]
    public Task NexumOpenTelemetry_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.OpenTelemetry.NexumTelemetryOptions).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumOpenTelemetry");
    }

    [Fact]
    public Task NexumExtensionsDI_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.Extensions.DependencyInjection.NexumServiceCollectionExtensions).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumExtensionsDI");
    }

    [Fact]
    public Task NexumExtensionsAspNetCore_PublicApi_ShouldNotChangeAsync()
    {
        var publicApi = typeof(Nexum.Extensions.AspNetCore.NexumProblemDetailsOptions).Assembly.GeneratePublicApi(s_options);
        return Verify(publicApi).UseMethodName("NexumExtensionsAspNetCore");
    }
}
