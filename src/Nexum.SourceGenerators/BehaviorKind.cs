namespace Nexum.SourceGenerators
{
    /// <summary>
    /// Identifies the kind of behavior discovered by the source generator.
    /// NOTE: Behaviors do not apply to notifications (unlike HandlerKind).
    /// </summary>
    internal enum BehaviorKind
    {
        Command,
        Query,
        StreamQuery
    }
}
