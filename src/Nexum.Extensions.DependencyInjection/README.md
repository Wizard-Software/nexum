# Nexum.Extensions.DependencyInjection

Microsoft DI integration for [Nexum](https://github.com/Wizard-Software/nexum). Provides `AddNexum()` with optional compile-time Source Generator acceleration.

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

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Dependency Injection](https://nexum.wizardsoftware.pl/articles/dependency-injection.html) for detailed usage.

## License

MIT
