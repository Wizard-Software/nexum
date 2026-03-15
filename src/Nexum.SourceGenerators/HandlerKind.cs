namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Identifies the kind of handler discovered by the source generator.
    /// </summary>
    internal enum HandlerKind
    {
        Command,
        Query,
        StreamQuery,
        Notification,
        StreamNotification
    }
}
