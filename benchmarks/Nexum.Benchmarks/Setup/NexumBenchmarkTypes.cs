#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Nexum.Abstractions;

namespace Nexum.Benchmarks.Setup;

// Command type returning Guid
public sealed record BenchCommand(string Name) : ICommand<Guid>;

// Query type returning string
public sealed record BenchQuery(int Id) : IQuery<string>;

// Stream query returning int items
public sealed record BenchStreamQuery(int Count) : IStreamQuery<int>;

// Notification type
public sealed record BenchNotification(string Message) : INotification;
