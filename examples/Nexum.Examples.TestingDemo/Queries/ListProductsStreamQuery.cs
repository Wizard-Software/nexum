using Nexum.Abstractions;
using Nexum.Examples.TestingDemo.Domain;

namespace Nexum.Examples.TestingDemo.Queries;

public sealed record ListProductsStreamQuery : IStreamQuery<Product>;
