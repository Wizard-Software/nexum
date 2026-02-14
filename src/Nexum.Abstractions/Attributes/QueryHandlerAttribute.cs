namespace Nexum.Abstractions;

/// <summary>
/// Marks a class as a query handler for Source Generator discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class QueryHandlerAttribute : Attribute;
