using Nexum.Abstractions;

namespace Nexum.Examples.TestingDemo.Notifications;

public sealed record ProductCreatedNotification(Guid Id, string Name) : INotification;
