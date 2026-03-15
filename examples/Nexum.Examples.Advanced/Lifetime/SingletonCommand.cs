using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

public sealed record SingletonCommand : ICommand<string>;
