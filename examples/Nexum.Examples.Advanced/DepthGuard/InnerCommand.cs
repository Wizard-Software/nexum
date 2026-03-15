using Nexum.Abstractions;

namespace Nexum.Examples.Advanced.DepthGuard;

public sealed record InnerCommand(bool ExceedDepth = false) : ICommand<string>;
