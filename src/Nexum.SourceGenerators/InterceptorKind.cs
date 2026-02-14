namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Identifies the kind of interceptor call-site discovered by the source generator.
    /// </summary>
    internal enum InterceptorKind
    {
        Command,
        Query,
        StreamQuery
    }
}
