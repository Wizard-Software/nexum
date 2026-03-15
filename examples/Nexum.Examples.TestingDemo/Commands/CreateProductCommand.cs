using Nexum.Abstractions;

namespace Nexum.Examples.TestingDemo.Commands;

public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<Guid>;
