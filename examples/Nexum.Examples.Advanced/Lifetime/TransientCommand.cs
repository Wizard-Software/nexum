using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.Lifetime;

public sealed record TransientCommand : ICommand<string>;
