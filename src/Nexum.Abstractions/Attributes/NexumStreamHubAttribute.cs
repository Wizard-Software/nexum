namespace Nexum.Abstractions;

/// <summary>
/// Marks a partial class as a Nexum stream hub for Source Generator discovery.
/// The Source Generator will generate concrete hub methods for each registered
/// <see cref="IStreamQueryHandler{TQuery,TResult}"/> and <see cref="IStreamNotificationHandler{TNotification,TItem}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NexumStreamHubAttribute : Attribute;
