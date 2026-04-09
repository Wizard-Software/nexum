# Nexum.Results

Result pattern for [Nexum](https://github.com/Wizard-Software/nexum). Type-safe error handling via `Result<T, TError>`.

## Installation

```bash
dotnet add package Nexum.Results
```

## Usage

```csharp
public sealed record CreateOrder(string CustomerId) : ICommand<Result<OrderId, CreateOrderError>>;

public enum CreateOrderError { CustomerNotFound, InsufficientCredit }
```

Handlers return `Result<T, TError>` instead of throwing exceptions — callers pattern-match on success or typed error.

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Result Pattern](https://nexum.wizardsoftware.pl/articles/results.html) for detailed usage.

## License

MIT
