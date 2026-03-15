namespace Nexum.Abstractions;

/// <summary>
/// Represents a streaming notification that produces an asynchronous sequence of <typeparamref name="TItem"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="INotification"/> (a one-shot domain event), a streaming notification
/// represents an intent to open a stream — subscribers receive <see cref="IAsyncEnumerable{T}"/>
/// of items. Multiple handler streams are merged via channel-based interleaving.
/// </para>
/// </remarks>
/// <typeparam name="TItem">The type of each element in the notification stream. Covariant.</typeparam>
public interface IStreamNotification<out TItem>;
