# Nexum.Extensions.AspNetCore

ASP.NET Core integration for [Nexum](https://github.com/Wizard-Software/nexum). Minimal API endpoint mapping and result-to-HTTP translation.

## Installation

```bash
dotnet add package Nexum.Extensions.AspNetCore
```

## Usage

```csharp
app.MapNexumEndpoints(endpoints =>
{
    endpoints.MapCommand<CreateOrder>("/orders", HttpMethod.Post);
    endpoints.MapQuery<GetOrder, OrderDto>("/orders/{id}");
});
```

## Documentation

Full documentation: **[nexum.wizardsoftware.pl](https://nexum.wizardsoftware.pl)**

See [ASP.NET Core Integration](https://nexum.wizardsoftware.pl/articles/aspnetcore-integration.html) for detailed usage.

## License

MIT
