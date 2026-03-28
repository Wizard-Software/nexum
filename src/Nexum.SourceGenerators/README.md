# Nexum.SourceGenerators

Roslyn Source Generators and analyzers for [Nexum](https://github.com/asawicki/nexum). Compile-time handler registration and diagnostics.

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

## License

MIT
