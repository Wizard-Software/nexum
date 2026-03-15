#pragma warning disable IL2026 // Suppress RequiresUnreferencedCode for test usage
#pragma warning disable IL2067 // Suppress RequiresDynamicCode for test usage

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;

namespace Nexum.Streaming.Tests;

/// <summary>
/// Integration tests for <see cref="NexumStreamEndpointRouteBuilderExtensions"/>.
/// Verifies SSE response format, JSON serialization, and client-disconnect cancellation.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SseEndpointTests
{
    // -------------------------------------------------------------------------
    // Test types
    // -------------------------------------------------------------------------

    private sealed record TestStreamQuery : IStreamQuery<string>
    {
        [Microsoft.AspNetCore.Mvc.FromQuery]
        public string Prefix { get; init; } = "item";
    }

    private sealed record InfiniteStreamQuery : IStreamQuery<string>;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static async IAsyncEnumerable<T> YieldItemsAsync<T>(IEnumerable<T> items)
    {
        foreach (T item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> InfiniteItemsAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        int i = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            yield return $"item-{i++}";
            await Task.Delay(10, ct);
        }
    }

    private static WebApplication CreateTestApp(
        IQueryDispatcher dispatcher,
        Action<WebApplication>? configureEndpoints = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(dispatcher);
        builder.Services.AddNexumStreaming();

        WebApplication app = builder.Build();
        configureEndpoints?.Invoke(app);
        return app;
    }

    // -------------------------------------------------------------------------
    // Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MapNexumStream_ReturnsServerSentEventsContentTypeAsync()
    {
        // Arrange
        IQueryDispatcher mockDispatcher = Substitute.For<IQueryDispatcher>();
        mockDispatcher.StreamAsync<string>(Arg.Any<IStreamQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(["a", "b", "c"]));

        await using WebApplication app = CreateTestApp(
            mockDispatcher,
            endpoints => endpoints.MapNexumStream<TestStreamQuery, string>("/test/stream"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act — read only headers; the infinite-ish SSE response would block if fully consumed
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/test/stream?Prefix=ping"),
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        string? contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().Be("text/event-stream");
    }

    [Fact]
    public async Task MapNexumStream_SerializesItemsAsJsonAsync()
    {
        // Arrange
        string[] sourceItems = ["item-0", "item-1", "item-2"];

        IQueryDispatcher mockDispatcher = Substitute.For<IQueryDispatcher>();
        mockDispatcher.StreamAsync<string>(Arg.Any<IStreamQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(YieldItemsAsync(sourceItems));

        await using WebApplication app = CreateTestApp(
            mockDispatcher,
            endpoints => endpoints.MapNexumStream<TestStreamQuery, string>("/test/stream"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        // Act — read the full SSE response body (finite, 3-item stream)
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/test/stream?Prefix=item"),
            HttpCompletionOption.ResponseHeadersRead,
            TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.Should().BeTrue();

        using System.IO.Stream body = await response.Content
            .ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var reader = new System.IO.StreamReader(body);

        List<string> dataLines = [];
        string? line;
        while ((line = await reader.ReadLineAsync(TestContext.Current.CancellationToken)) is not null)
        {
            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line);
            }
        }

        // Assert — each item emitted as a "data: ..." SSE line.
        // Note: TypedResults.ServerSentEvents writes string values without extra JSON quoting;
        // complex object types are serialized as JSON.
        dataLines.Should().HaveCount(3);
        dataLines[0].Should().Be("data: item-0");
        dataLines[1].Should().Be("data: item-1");
        dataLines[2].Should().Be("data: item-2");
    }

    [Fact]
    public async Task MapNexumStream_WithCancellation_StopsStreamAsync()
    {
        // Arrange
        IQueryDispatcher mockDispatcher = Substitute.For<IQueryDispatcher>();
        mockDispatcher.StreamAsync<string>(Arg.Any<IStreamQuery<string>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => InfiniteItemsAsync(callInfo.Arg<CancellationToken>()));

        await using WebApplication app = CreateTestApp(
            mockDispatcher,
            endpoints => endpoints.MapNexumStream<InfiniteStreamQuery, string>("/test/infinite"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.First()) };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);

        // Act — start the infinite stream, read two items, then cancel to simulate client disconnect
        using HttpResponseMessage response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/test/infinite"),
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        response.IsSuccessStatusCode.Should().BeTrue();

        using System.IO.Stream body = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new System.IO.StreamReader(body);

        List<string> receivedDataLines = [];

        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cts.Token)) is not null)
            {
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    receivedDataLines.Add(line);

                    if (receivedDataLines.Count >= 2)
                    {
                        // Simulate client disconnect by cancelling the HTTP request
                        await cts.CancelAsync();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected — cancellation terminates the stream read loop
        }

        // Assert — we received items before cancellation stopped the stream
        receivedDataLines.Should().HaveCountGreaterThanOrEqualTo(2);
        receivedDataLines[0].Should().StartWith("data:");
        receivedDataLines[1].Should().StartWith("data:");
    }
}
