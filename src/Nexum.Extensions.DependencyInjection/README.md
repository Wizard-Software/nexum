# Nexum.Extensions.DependencyInjection

Microsoft DI integration for [Nexum](https://github.com/asawicki/nexum). Provides `AddNexum()` with optional compile-time Source Generator acceleration.

## Installation

```bash
dotnet add package Nexum.Extensions.DependencyInjection
```

## Usage

```csharp
builder.Services.AddNexum(options =>
{
    options.AddHandlersFromAssemblyOf<CreateOrderHandler>();
});
```

The package bundles `Nexum.SourceGenerators` — handler registration is optimized at compile time automatically.

## License

MIT
