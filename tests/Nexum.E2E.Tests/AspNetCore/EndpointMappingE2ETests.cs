#pragma warning disable IL2026
#pragma warning disable IL3050

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.Extensions.AspNetCore;
using Nexum.Extensions.DependencyInjection;

namespace Nexum.E2E.Tests.AspNetCore;

[Trait("Category", "E2E")]
public sealed class EndpointMappingE2ETests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddNexum(assemblies: typeof(EndpointMappingE2ETests).Assembly);
        builder.Services.AddNexumAspNetCore();
        builder.Services.AddSingleton(new ConcurrentDictionary<Guid, TicketTestDto>());

        _app = builder.Build();
        _app.UseNexum();
        _app.MapNexumCommand<CreateTicketTestCommand, Guid>("/api/tickets");
        _app.MapNexumQuery<GetTicketTestQuery, TicketTestDto?>("/api/tickets/{id}");
        _app.MapNexumCommand<CloseTicketTestCommand>("/api/tickets/{id}/close");

        _app.Urls.Add("http://127.0.0.1:0");
        await _app.StartAsync(TestContext.Current.CancellationToken);

        var server = _app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        var baseUrl = addresses!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync(CancellationToken.None);
        await _app.DisposeAsync();
    }

    // E2E-090: POST /api/tickets with title -> 200 OK with Guid
    [Fact]
    public async Task PostTicket_ValidCommand_Returns200WithGuid()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new { Title = "E2E Test Ticket" },
            TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = await response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken);
        id.Should().NotBeEmpty();
    }

    // E2E-091: GET /api/tickets/{id} -> 200 OK with TicketTestDto JSON
    [Fact]
    public async Task GetTicket_AfterCreation_Returns200WithTicket()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange - create a ticket first
        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new { Title = "Query Test" }, ct);
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>(ct);

        // Act
        var response = await _client.GetAsync($"/api/tickets/{id}", ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var ticket = await response.Content.ReadFromJsonAsync<TicketTestDto>(ct);
        ticket.Should().NotBeNull();
        ticket!.Title.Should().Be("Query Test");
        ticket.Status.Should().Be("Open");
    }

    // E2E-092: PUT /api/tickets/{id}/close (void command) -> 204 No Content
    [Fact]
    public async Task CloseTicket_VoidCommand_Returns204NoContent()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange - create a ticket first
        var createResponse = await _client.PostAsJsonAsync("/api/tickets", new { Title = "Close Test" }, ct);
        var id = await createResponse.Content.ReadFromJsonAsync<Guid>(ct);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/tickets/{id}/close", new { Id = id }, ct);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // E2E-095: GET /api/tickets/{nonexistent} -> handler throws -> middleware returns 500
    [Fact]
    public async Task GetTicket_NotFound_ReturnsServerError()
    {
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/tickets/{nonExistentId}", ct);

        // Assert - NexumMiddleware catches handler exception -> 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}

// --- Inline types for the test web app ---

public sealed record TicketTestDto(Guid Id, string Title, string Status);

public sealed record CreateTicketTestCommand(string Title) : ICommand<Guid>;
public sealed record GetTicketTestQuery(Guid Id) : IQuery<TicketTestDto?>;
public sealed record CloseTicketTestCommand(Guid Id) : IVoidCommand;

public sealed class CreateTicketTestHandler(ConcurrentDictionary<Guid, TicketTestDto> store)
    : ICommandHandler<CreateTicketTestCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateTicketTestCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        store[id] = new TicketTestDto(id, command.Title, "Open");
        return ValueTask.FromResult(id);
    }
}

public sealed class GetTicketTestHandler(ConcurrentDictionary<Guid, TicketTestDto> store)
    : IQueryHandler<GetTicketTestQuery, TicketTestDto?>
{
    public ValueTask<TicketTestDto?> HandleAsync(GetTicketTestQuery query, CancellationToken ct = default)
    {
        if (!store.TryGetValue(query.Id, out TicketTestDto? ticket))
        {
            throw new KeyNotFoundException($"Ticket '{query.Id}' not found.");
        }

        return ValueTask.FromResult<TicketTestDto?>(ticket);
    }
}

public sealed class CloseTicketTestHandler(ConcurrentDictionary<Guid, TicketTestDto> store)
    : ICommandHandler<CloseTicketTestCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(CloseTicketTestCommand command, CancellationToken ct = default)
    {
        if (store.TryGetValue(command.Id, out TicketTestDto? ticket))
        {
            store[command.Id] = ticket with { Status = "Closed" };
        }

        return ValueTask.FromResult(Unit.Value);
    }
}
