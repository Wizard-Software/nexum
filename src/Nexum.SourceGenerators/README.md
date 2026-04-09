# Nexum.SourceGenerators

Roslyn Source Generators and analyzers for [Nexum](https://github.com/Wizard-Software/nexum). Compile-time handler registration and diagnostics.

## What's inside

- **Tier 1** — DI registration code generation for `AddNexum()` + compile-time diagnostics (NEXUM001–NEXUM004)
- **Tier 2** — Compiled pipeline delegates (monomorphized dispatch)
- **Tier 3** — Roslyn Interceptors for call-site replacement

Uses `ForAttributeWithMetadataName` for fast handler discovery. This package is **optional** — Nexum works standalone without it.

## Installation

```bash
dotnet add package Nexum.SourceGenerators
```

> **Note:** If you use `Nexum.Extensions.DependencyInjection`, the Source Generator is already bundled — no need to install separately.

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [Source Generators](https://nexum.wizardsoftware.pl/articles/source-generators.html) for the tiered architecture, diagnostics reference, and interceptor setup.

## License

MIT
