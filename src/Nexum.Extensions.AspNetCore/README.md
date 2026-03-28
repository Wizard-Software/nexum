# Nexum.Extensions.AspNetCore

ASP.NET Core integration for [Nexum](https://github.com/asawicki/nexum). Minimal API endpoint mapping and result-to-HTTP translation.

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

## License

MIT
