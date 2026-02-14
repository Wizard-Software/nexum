namespace Nexum.Results.FluentValidation;

/// <summary>
/// Factory for creating failure Result instances from <see cref="NexumError"/>.
/// Register a custom implementation for NativeAOT/trimming support,
/// or use the default reflection-based implementation registered by
/// <see cref="Extensions.DependencyInjection.NexumFluentValidationServiceCollectionExtensions.AddNexumFluentValidation"/>.
/// </summary>
public interface IResultFailureFactory
{
    /// <summary>Checks if this factory can create a failure result for the given type.</summary>
    bool CanCreate(Type resultType);

    /// <summary>Creates a failure result. The returned object must be castable to the resultType.</summary>
    object CreateFailure(Type resultType, NexumError error);
}
