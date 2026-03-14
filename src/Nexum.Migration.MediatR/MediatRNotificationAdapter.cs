using Nexum.Abstractions;
using MediatRNS = global::MediatR;

namespace Nexum.Migration.MediatR;

/// <summary>
/// Nexum notification handler that delegates to an existing MediatR <see cref="global::MediatR.INotificationHandler{TNotification}"/>.
/// Enables gradual migration: the notification type implements both Nexum <see cref="INotification"/>
/// and MediatR <see cref="global::MediatR.INotification"/>.
/// </summary>
/// <typeparam name="TNotification">The notification type that implements both Nexum and MediatR INotification.</typeparam>
public sealed class MediatRNotificationAdapter<TNotification>
    : Nexum.Abstractions.INotificationHandler<TNotification>
    where TNotification : Nexum.Abstractions.INotification, MediatRNS.INotification
{
    private readonly MediatRNS.INotificationHandler<TNotification> _mediatRHandler;

    /// <summary>
    /// Initializes a new instance of <see cref="MediatRNotificationAdapter{TNotification}"/>.
    /// </summary>
    /// <param name="mediatRHandler">The MediatR notification handler to delegate to.</param>
    public MediatRNotificationAdapter(MediatRNS.INotificationHandler<TNotification> mediatRHandler)
    {
        ArgumentNullException.ThrowIfNull(mediatRHandler);
        _mediatRHandler = mediatRHandler;
    }

    /// <inheritdoc />
    public async ValueTask HandleAsync(TNotification notification, CancellationToken ct = default)
    {
        await _mediatRHandler.Handle(notification, ct).ConfigureAwait(false);
    }
}
