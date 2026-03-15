using Nexum.Abstractions;
using Nexum.Examples.SourceGenerators.Domain;

namespace Nexum.Examples.SourceGenerators.Queries;

public sealed record GetInvoiceQuery(Guid Id) : IQuery<Invoice>;
