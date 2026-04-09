# Source Generators

`Nexum.SourceGenerators` is an **optional** Roslyn incremental source generator that accelerates dispatch. The runtime in `Nexum` works standalone without it — you can start without the generator and opt in later when you need the extra performance or compile-time safety.

## Tiered architecture

The generator ships three tiers, each building on the previous one.

| Tier | Technique | Overhead vs Runtime | Allocations |
|------|-----------|---------------------|-------------|
| 1 — Runtime | Polymorphic handler cache, reflection-based resolution | baseline | 0 B |
| 2 — Compiled Pipeline | Source-generated pipeline delegates, monomorphized dispatch | 1.32× faster | 0 B |
| 3 — Interceptors | Roslyn Interceptors replace `DispatchAsync` call sites at compile time | 1.52× faster | 0 B |

All tiers produce **zero allocations** on the hot path. Tier 3 uses Roslyn Interceptors (stable since .NET 9.0.2xx SDK) to eliminate virtual dispatch entirely — the compiler rewrites the call site to point directly at the generated handler wrapper.

## What the generator does

### Tier 1 — DI registration and diagnostics

- Scans the compilation for types annotated with `[CommandHandler]`, `[QueryHandler]`, `[StreamQueryHandler]`, `[NotificationHandler]` using `ForAttributeWithMetadataName` (about 99× faster than traditional `SyntaxReceiver`).
- Emits a `services.AddNexum()` partial method that registers every handler it found.
- Reports compile-time diagnostics:
  - `NEXUM001` — handler type must not be abstract.
  - `NEXUM002` — handler must implement the correct interface for its attribute.
  - `NEXUM003` — duplicate handler registration for the same command/query.
  - `NEXUM004` — notification exception handler must target a valid notification type.

### Tier 2 — Compiled pipelines

For every command/query the generator walks the DI graph and emits a strongly typed delegate chain. The pipeline builder is bypassed entirely at dispatch time.

### Tier 3 — Interceptors

The generator emits an `InterceptsLocationAttribute` on a method that replaces the `ICommandDispatcher.DispatchAsync` call site. The runtime still ships the virtual dispatcher as a fallback for cases the compiler can't statically resolve (reflection-driven dispatch, dynamic command types, etc.).

## Enabling the generator

```bash
dotnet add package Nexum.SourceGenerators
```

That's it. The package is marked `DevelopmentDependency="true"` and ships as an analyzer — no runtime assembly, no NuGet graph pollution for your consumers.

Enable interceptors (Tier 3) in your project file:

```xml
<PropertyGroup>
  <InterceptorsPreviewNamespaces>$(InterceptorsPreviewNamespaces);Nexum.Generated</InterceptorsPreviewNamespaces>
</PropertyGroup>
```

## Source Generator vs Runtime — when to choose which?

- **Start with Runtime.** It's fast enough for 99% of apps (1.5× faster than MediatR already).
- **Add the generator** when you want compile-time safety for handler discovery, or when profiling shows dispatch overhead matters.
- **Enable interceptors** for latency-critical hot paths where every nanosecond counts.

The dual-path design (System.Text.Json pattern) means your code does not change when you adopt or remove the generator — only the `.csproj` does.
