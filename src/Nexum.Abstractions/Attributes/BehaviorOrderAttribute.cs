namespace Nexum.Abstractions;

/// <summary>
/// Specifies the order in which a behavior executes in the pipeline.
/// Lower values execute first (outermost in the Russian doll model).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BehaviorOrderAttribute(int order) : Attribute
{
    /// <summary>Gets the pipeline execution order. Lower values execute first.</summary>
    public int Order { get; } = order;
}
