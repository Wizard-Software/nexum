using Nexum.Abstractions;

namespace Nexum.Testing.Tests;

[Trait("Category", "Unit")]
public sealed class NexumAssertionExtensionsTests
{
    // === Command assertions ===

    [Fact]
    public async Task ShouldHaveDispatched_WhenDispatched_DoesNotThrowAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<AssertTestCommand, string>().Returns("ok");
        await dispatcher.DispatchAsync(new AssertTestCommand("test"), CancellationToken.None);

        // Act & Assert — no exception expected
        dispatcher.ShouldHaveDispatched<AssertTestCommand>();
    }

    [Fact]
    public void ShouldHaveDispatched_WhenNotDispatched_ThrowsAssertionException()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();

        // Act
        var act = () => dispatcher.ShouldHaveDispatched<AssertTestCommand>();

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestCommand*");
    }

    [Fact]
    public async Task ShouldHaveDispatched_WithPredicate_MatchesCorrectCommandAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<AssertTestCommand, string>().Returns("ok");
        dispatcher.Setup<AssertOtherCommand, int>().Returns(0);
        await dispatcher.DispatchAsync(new AssertTestCommand("alpha"), CancellationToken.None);
        await dispatcher.DispatchAsync(new AssertTestCommand("beta"), CancellationToken.None);
        await dispatcher.DispatchAsync(new AssertOtherCommand(99), CancellationToken.None);

        // Act & Assert — predicate matches "beta"
        dispatcher.ShouldHaveDispatched<AssertTestCommand>(cmd => cmd.Value == "beta");
    }

    [Fact]
    public async Task ShouldHaveDispatched_WithPredicate_NoMatch_ThrowsAssertionExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<AssertTestCommand, string>().Returns("ok");
        await dispatcher.DispatchAsync(new AssertTestCommand("alpha"), CancellationToken.None);

        // Act
        var act = () => dispatcher.ShouldHaveDispatched<AssertTestCommand>(cmd => cmd.Value == "notexist");

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestCommand*predicate*");
    }

    [Fact]
    public async Task ShouldHaveDispatched_WithTimes_VerifiesExactCountAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<AssertTestCommand, string>().Returns("ok");
        await dispatcher.DispatchAsync(new AssertTestCommand("a"), CancellationToken.None);
        await dispatcher.DispatchAsync(new AssertTestCommand("b"), CancellationToken.None);
        await dispatcher.DispatchAsync(new AssertTestCommand("c"), CancellationToken.None);

        // Act & Assert — times=3 passes
        dispatcher.ShouldHaveDispatched<AssertTestCommand>(3);

        // times=2 throws
        var act = () => dispatcher.ShouldHaveDispatched<AssertTestCommand>(2);
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*2*AssertTestCommand*3*");
    }

    [Fact]
    public async Task ShouldNotHaveDispatched_WhenDispatched_ThrowsAssertionExceptionAsync()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();
        dispatcher.Setup<AssertTestCommand, string>().Returns("ok");
        await dispatcher.DispatchAsync(new AssertTestCommand("x"), CancellationToken.None);

        // Act
        var act = () => dispatcher.ShouldNotHaveDispatched<AssertTestCommand>();

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestCommand*");
    }

    [Fact]
    public void ShouldNotHaveDispatched_WhenNotDispatched_DoesNotThrow()
    {
        // Arrange
        var dispatcher = new FakeCommandDispatcher();

        // Act & Assert — no exception expected
        dispatcher.ShouldNotHaveDispatched<AssertTestCommand>();
    }

    // === Query assertions ===

    [Fact]
    public async Task ShouldHaveDispatched_Query_WhenDispatched_DoesNotThrowAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<AssertTestQuery, string>().Returns("result");
        await dispatcher.DispatchAsync(new AssertTestQuery("filter"), CancellationToken.None);

        // Act & Assert — no exception expected
        dispatcher.ShouldHaveDispatched<AssertTestQuery>();
    }

    [Fact]
    public void ShouldHaveDispatched_Query_WhenNotDispatched_ThrowsAssertionException()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();

        // Act
        var act = () => dispatcher.ShouldHaveDispatched<AssertTestQuery>();

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestQuery*");
    }

    [Fact]
    public async Task ShouldHaveDispatched_QueryWithPredicate_MatchesCorrectQueryAsync()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();
        dispatcher.Setup<AssertTestQuery, string>().Returns("result");
        await dispatcher.DispatchAsync(new AssertTestQuery("foo"), CancellationToken.None);
        await dispatcher.DispatchAsync(new AssertTestQuery("bar"), CancellationToken.None);

        // Act & Assert — predicate matches "bar"
        dispatcher.ShouldHaveDispatched<AssertTestQuery>(q => q.Filter == "bar");
    }

    [Fact]
    public void ShouldNotHaveDispatched_Query_WhenNotDispatched_DoesNotThrow()
    {
        // Arrange
        var dispatcher = new FakeQueryDispatcher();

        // Act & Assert — no exception expected
        dispatcher.ShouldNotHaveDispatched<AssertTestQuery>();
    }

    // === Notification assertions ===

    [Fact]
    public async Task ShouldHavePublished_WhenPublished_DoesNotThrowAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        await collector.PublishAsync(new AssertTestNotification("event"), ct: CancellationToken.None);

        // Act & Assert — no exception expected
        collector.ShouldHavePublished<AssertTestNotification>();
    }

    [Fact]
    public void ShouldHavePublished_WhenNotPublished_ThrowsAssertionException()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();

        // Act
        var act = () => collector.ShouldHavePublished<AssertTestNotification>();

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestNotification*");
    }

    [Fact]
    public async Task ShouldHavePublished_WithPredicate_MatchesCorrectNotificationAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        await collector.PublishAsync(new AssertTestNotification("first"), ct: CancellationToken.None);
        await collector.PublishAsync(new AssertTestNotification("second"), ct: CancellationToken.None);

        // Act & Assert — predicate matches "second"
        collector.ShouldHavePublished<AssertTestNotification>(n => n.Message == "second");
    }

    [Fact]
    public void ShouldNotHavePublished_WhenNotPublished_DoesNotThrow()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();

        // Act & Assert — no exception expected
        collector.ShouldNotHavePublished<AssertTestNotification>();
    }

    [Fact]
    public async Task ShouldNotHavePublished_WhenPublished_ThrowsAssertionExceptionAsync()
    {
        // Arrange
        var collector = new InMemoryNotificationCollector();
        await collector.PublishAsync(new AssertTestNotification("oops"), ct: CancellationToken.None);

        // Act
        var act = () => collector.ShouldNotHavePublished<AssertTestNotification>();

        // Assert
        act.Should().Throw<NexumAssertionException>()
            .WithMessage("*AssertTestNotification*");
    }
}

internal sealed record AssertTestCommand(string Value) : ICommand<string>;
internal sealed record AssertOtherCommand(int Number) : ICommand<int>;
internal sealed record AssertTestQuery(string Filter) : IQuery<string>;
internal sealed record AssertTestNotification(string Message) : INotification;
