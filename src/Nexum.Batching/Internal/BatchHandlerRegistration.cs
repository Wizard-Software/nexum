namespace Nexum.Batching.Internal;

/// <summary>
/// Registration metadata for a batch query handler discovered during assembly scanning.
/// Stored in DI as <c>IEnumerable&lt;BatchHandlerRegistration&gt;</c> for buffer creation.
/// </summary>
internal sealed record BatchHandlerRegistration(
    Type QueryType,
    Type KeyType,
    Type ResultType,
    Type HandlerType);
