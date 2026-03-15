using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

public sealed record ScopedCommand : ICommand<string>;
