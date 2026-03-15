using Nexum.Abstractions;

namespace Nexum.Examples.SourceGenerators.Notifications;

public sealed record InvoiceCreatedNotification(Guid InvoiceId, string Customer, decimal Amount) : INotification;
