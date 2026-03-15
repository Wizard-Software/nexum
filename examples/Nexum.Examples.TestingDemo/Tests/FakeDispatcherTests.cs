using AwesomeAssertions;
using Nexum.Testing;
using Xunit;
using Nexum.Examples.TestingDemo.Commands;
using Nexum.Examples.TestingDemo.Domain;
using Nexum.Examples.TestingDemo.Queries;

namespace Nexum.Examples.TestingDemo.Tests;

/// <summary>
/// Demonstrates FakeCommandDispatcher and FakeQueryDispatcher for pure unit tests.
/// No DI container or real handlers — configure return values directly via fluent Setup().
/// </summary>
public sealed class FakeDispatcherTests
{
    [Fact]
    public async Task FakeCommandDispatcher_Setup_Returns_ReturnsConfiguredValueAsync()
    {
        // FakeCommandDispatcher replaces ICommandDispatcher — no DI, no handlers
        var dispatcher = new FakeCommandDispatcher();
        var expectedId = Guid.NewGuid();

        // Setup<TCommand, TResult>().Returns(value) configures the fake for that command type
        dispatcher.Setup<CreateProductCommand, Guid>().Returns(expectedId);

        var result = await dispatcher.DispatchAsync(
            new CreateProductCommand("Widget", 9.99m),
            CancellationToken.None);

        result.Should().Be(expectedId);
    }

    [Fact]
    public async Task FakeCommandDispatcher_Setup_Throws_ThrowsExceptionAsync()
    {
        var dispatcher = new FakeCommandDispatcher();

        // Throws<TException>() causes the dispatcher to throw when the command is dispatched
        dispatcher.Setup<CreateProductCommand, Guid>().Throws<InvalidOperationException>();

        var act = async () => await dispatcher.DispatchAsync(
            new CreateProductCommand("Broken", 0m),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FakeQueryDispatcher_Setup_Returns_ReturnsConfiguredValueAsync()
    {
        var dispatcher = new FakeQueryDispatcher();
        var id = Guid.NewGuid();
        var expectedProduct = new Product(id, "Gadget", 49.99m);

        dispatcher.Setup<GetProductQuery, Product?>().Returns(expectedProduct);

        var result = await dispatcher.DispatchAsync(
            new GetProductQuery(id),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Gadget");
        result.Price.Should().Be(49.99m);
    }

    [Fact]
    public async Task FakeQueryDispatcher_SetupStream_Returns_YieldsItemsAsync()
    {
        // SetupStream<TQuery, TResult>() configures streaming for IStreamQuery<T> types
        var dispatcher = new FakeQueryDispatcher();
        var item1 = new Product(Guid.NewGuid(), "Alpha", 10m);
        var item2 = new Product(Guid.NewGuid(), "Beta", 20m);
        var item3 = new Product(Guid.NewGuid(), "Gamma", 30m);

        // Returns(params TResult[]) yields the items as an async stream
        dispatcher.SetupStream<ListProductsStreamQuery, Product>().Returns(item1, item2, item3);

        var results = new List<Product>();
        await foreach (var product in dispatcher.StreamAsync(
            new ListProductsStreamQuery(),
            CancellationToken.None))
        {
            results.Add(product);
        }

        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Alpha");
        results[1].Name.Should().Be("Beta");
        results[2].Name.Should().Be("Gamma");
    }
}
