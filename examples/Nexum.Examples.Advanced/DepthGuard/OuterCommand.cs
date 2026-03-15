using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.DepthGuard;

public sealed record OuterCommand(bool ExceedDepth = false) : ICommand<string>;
