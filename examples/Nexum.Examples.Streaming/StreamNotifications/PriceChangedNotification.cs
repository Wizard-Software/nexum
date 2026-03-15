using Nexum.Abstractions;
using Nexum.Examples.Streaming.Domain;

namespace Nexum.Examples.Streaming.StreamNotifications;

/// <summary>
/// Stream notification that signals a price change event.
/// Subscribers receive an IAsyncEnumerable of PriceUpdate items.
///
/// Unlike INotification (one-shot fire-and-forget), IStreamNotification opens
/// a bidirectional channel — the notification triggers handlers that each produce
/// their own async streams; all streams are merged via channel-based interleaving.
/// </summary>
public record PriceChangedNotification(string Symbol) : IStreamNotification<PriceUpdate>;
