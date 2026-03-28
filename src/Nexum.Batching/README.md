# Nexum.Batching

Query batching and DataLoader-style deduplication for [Nexum](https://github.com/asawicki/nexum).

## Installation

```bash
dotnet add package Nexum.Batching
```

## Usage

Batch multiple identical queries within a scope into a single handler execution, reducing database round-trips. Ideal for GraphQL resolvers and N+1 scenarios.

## License

MIT
