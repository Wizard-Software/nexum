# Nexum.Results

Result pattern for [Nexum](https://github.com/asawicki/nexum). Type-safe error handling via `Result<T, TError>`.

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

## License

MIT
