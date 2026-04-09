# Nexum.Results.FluentValidation

FluentValidation integration for [Nexum Results](https://github.com/Wizard-Software/nexum). Validation behaviors returning typed errors.

## Installation

```bash
dotnet add package Nexum.Results.FluentValidation
```

## Usage

```csharp
builder.Services.AddNexum(options =>
{
    options.AddFluentValidation();
});
```

Validators are automatically discovered and run as pipeline behaviors, returning validation errors as typed `Result` failures instead of throwing exceptions.

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Result Pattern](https://nexum.wizardsoftware.pl/articles/results.html) for detailed usage, including the FluentValidation integration.

## License

MIT
