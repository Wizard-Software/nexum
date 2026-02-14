namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class ExceptionHandlerTests
{
    // Test helpers - Commands
    private record CreateOrderCommand(string Product) : ICommand<Guid>;
    private record DeleteOrderCommand(Guid Id) : IVoidCommand;

    // Test helpers - Queries
    private record GetOrderQuery(Guid Id) : IQuery<string>;

    // Test helpers - Notifications
    private record OrderCreatedNotification(Guid OrderId) : INotification;

    // Test helpers - Exception hierarchy
    private class CustomException : Exception;
    private class SpecificException : CustomException;

    // Test helper implementations
    private class GenericCommandExceptionHandler : ICommandExceptionHandler<ICommand, Exception>
    {
        public ValueTask HandleAsync(ICommand command, Exception exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class SpecificCommandExceptionHandler : ICommandExceptionHandler<CreateOrderCommand, SpecificException>
    {
        public ValueTask HandleAsync(CreateOrderCommand command, SpecificException exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class GenericQueryExceptionHandler : IQueryExceptionHandler<IQuery, Exception>
    {
        public ValueTask HandleAsync(IQuery query, Exception exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class SpecificQueryExceptionHandler : IQueryExceptionHandler<GetOrderQuery, SpecificException>
    {
        public ValueTask HandleAsync(GetOrderQuery query, SpecificException exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class GenericNotificationExceptionHandler : INotificationExceptionHandler<INotification, Exception>
    {
        public ValueTask HandleAsync(INotification notification, Exception exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    private class SpecificNotificationExceptionHandler : INotificationExceptionHandler<OrderCreatedNotification, SpecificException>
    {
        public ValueTask HandleAsync(OrderCreatedNotification notification, SpecificException exception, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }

    // --- ICommandExceptionHandler tests ---

    [Fact]
    public void ICommandExceptionHandler_ConstrainsCommandToICommand()
    {
        var handler = new GenericCommandExceptionHandler();

        handler.Should().BeAssignableTo<ICommandExceptionHandler<ICommand, Exception>>();
    }

    [Fact]
    public void ICommandExceptionHandler_IsContravariantInBothParameters()
    {
        // Generic handler (ICommand, Exception) can be assigned to specific types
        ICommandExceptionHandler<CreateOrderCommand, SpecificException> specificHandler = new GenericCommandExceptionHandler();

        specificHandler.Should().NotBeNull();
        specificHandler.Should().BeAssignableTo<ICommandExceptionHandler<CreateOrderCommand, SpecificException>>();
    }

    [Fact]
    public void ICommandExceptionHandler_CatchAllPattern_Works()
    {
        ICommandExceptionHandler<ICommand, Exception> catchAll = new GenericCommandExceptionHandler();

        catchAll.Should().BeAssignableTo<ICommandExceptionHandler<ICommand, Exception>>();
    }

    [Fact]
    public void ICommandExceptionHandler_HandleAsync_ReturnsValueTask()
    {
        var handler = new GenericCommandExceptionHandler();
        var command = new CreateOrderCommand("Widget");
        var exception = new Exception("Test");

        var result = handler.HandleAsync(command, exception, TestContext.Current.CancellationToken);

        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    // --- IQueryExceptionHandler tests ---

    [Fact]
    public void IQueryExceptionHandler_ConstrainsQueryToIQuery()
    {
        var handler = new GenericQueryExceptionHandler();

        handler.Should().BeAssignableTo<IQueryExceptionHandler<IQuery, Exception>>();
    }

    [Fact]
    public void IQueryExceptionHandler_IsContravariantInBothParameters()
    {
        IQueryExceptionHandler<GetOrderQuery, SpecificException> specificHandler = new GenericQueryExceptionHandler();

        specificHandler.Should().NotBeNull();
        specificHandler.Should().BeAssignableTo<IQueryExceptionHandler<GetOrderQuery, SpecificException>>();
    }

    [Fact]
    public void IQueryExceptionHandler_CatchAllPattern_Works()
    {
        IQueryExceptionHandler<IQuery, Exception> catchAll = new GenericQueryExceptionHandler();

        catchAll.Should().BeAssignableTo<IQueryExceptionHandler<IQuery, Exception>>();
    }

    [Fact]
    public void IQueryExceptionHandler_HandleAsync_ReturnsValueTask()
    {
        var handler = new GenericQueryExceptionHandler();
        var query = new GetOrderQuery(Guid.NewGuid());
        var exception = new Exception("Test");

        var result = handler.HandleAsync(query, exception, TestContext.Current.CancellationToken);

        result.IsCompletedSuccessfully.Should().BeTrue();
    }

    // --- INotificationExceptionHandler tests ---

    [Fact]
    public void INotificationExceptionHandler_ConstrainsNotificationToINotification()
    {
        var handler = new GenericNotificationExceptionHandler();

        handler.Should().BeAssignableTo<INotificationExceptionHandler<INotification, Exception>>();
    }

    [Fact]
    public void INotificationExceptionHandler_IsContravariantInBothParameters()
    {
        INotificationExceptionHandler<OrderCreatedNotification, SpecificException> specificHandler = new GenericNotificationExceptionHandler();

        specificHandler.Should().NotBeNull();
        specificHandler.Should().BeAssignableTo<INotificationExceptionHandler<OrderCreatedNotification, SpecificException>>();
    }

    [Fact]
    public void INotificationExceptionHandler_CatchAllPattern_Works()
    {
        INotificationExceptionHandler<INotification, Exception> catchAll = new GenericNotificationExceptionHandler();

        catchAll.Should().BeAssignableTo<INotificationExceptionHandler<INotification, Exception>>();
    }

    [Fact]
    public void INotificationExceptionHandler_HandleAsync_ReturnsValueTask()
    {
        var handler = new GenericNotificationExceptionHandler();
        var notification = new OrderCreatedNotification(Guid.NewGuid());
        var exception = new Exception("Test");

        var result = handler.HandleAsync(notification, exception, TestContext.Current.CancellationToken);

        result.IsCompletedSuccessfully.Should().BeTrue();
    }
}
