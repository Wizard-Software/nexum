using Nexum.Abstractions;

namespace Nexum.Examples.Behaviors.Queries;

public sealed record ListProductsStreamQuery(decimal MinPrice) : IStreamQuery<string>;
