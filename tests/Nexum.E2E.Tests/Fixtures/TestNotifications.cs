using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

public sealed record ItemCreatedNotification(Guid Id, string Name) : INotification;

// Notification where one handler throws
public sealed record FaultyNotification(string Message) : INotification;
