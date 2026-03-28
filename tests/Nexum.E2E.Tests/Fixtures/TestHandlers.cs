using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

// --- Command Handlers ---

public sealed class CreateItemHandler(ConcurrentDictionary<Guid, ItemDto> store)
    : ICommandHandler<CreateItemCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(CreateItemCommand command, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        store[id] = new ItemDto(id, command.Name);
        return ValueTask.FromResult(id);
    }
}

public sealed class DeleteItemHandler(ConcurrentDictionary<Guid, ItemDto> store)
    : ICommandHandler<DeleteItemCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(DeleteItemCommand command, CancellationToken ct = default)
    {
        store.TryRemove(command.Id, out _);
        return ValueTask.FromResult(Unit.Value);
    }
}

public sealed class RecursiveCommandHandler(ICommandDispatcher dispatcher)
    : ICommandHandler<RecursiveCommand, string>
{
    public async ValueTask<string> HandleAsync(RecursiveCommand command, CancellationToken ct = default)
    {
        if (command.ShouldRecurse)
        {
            var inner = await dispatcher.DispatchAsync(new RecursiveCommand(ShouldRecurse: true), ct).ConfigureAwait(false);
            return $"Outer→{inner}";
        }

        return "Leaf";
    }
}

public sealed class FailingCommandHandler : ICommandHandler<FailingCommand, string>
{
    public ValueTask<string> HandleAsync(FailingCommand command, CancellationToken ct = default)
    {
        throw new InvalidOperationException(command.Message);
    }
}

public sealed class SlowCommandHandler : ICommandHandler<SlowCommand, string>
{
    public async ValueTask<string> HandleAsync(SlowCommand command, CancellationToken ct = default)
    {
        await Task.Delay(command.DelayMs, ct).ConfigureAwait(false);
        return "completed";
    }
}

public sealed class TrackedCommandHandler : ICommandHandler<TrackedCommand, string>
{
    public ValueTask<string> HandleAsync(TrackedCommand command, CancellationToken ct = default)
    {
        return ValueTask.FromResult($"handled:{command.Input}");
    }
}

// --- Lifetime handlers ---

public sealed class TransientTestHandler : ICommandHandler<TransientTestCommand, Guid>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<Guid> HandleAsync(TransientTestCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId);
}

public sealed class ScopedTestHandler : ICommandHandler<ScopedTestCommand, Guid>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<Guid> HandleAsync(ScopedTestCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId);
}

public sealed class SingletonTestHandler : ICommandHandler<SingletonTestCommand, Guid>
{
    private readonly Guid _instanceId = Guid.NewGuid();

    public ValueTask<Guid> HandleAsync(SingletonTestCommand command, CancellationToken ct = default)
        => ValueTask.FromResult(_instanceId);
}

// --- Polymorphic handlers ---

public sealed class CreditCardPaymentHandler : ICommandHandler<CreditCardPaymentCommand, string>
{
    public ValueTask<string> HandleAsync(CreditCardPaymentCommand command, CancellationToken ct = default)
        => ValueTask.FromResult($"CreditCard:{command.Amount:F2}");
}

public sealed class BankTransferPaymentHandler : ICommandHandler<BankTransferPaymentCommand, string>
{
    public ValueTask<string> HandleAsync(BankTransferPaymentCommand command, CancellationToken ct = default)
        => ValueTask.FromResult($"BankTransfer:{command.Amount:F2}");
}

// --- Query Handlers ---

public sealed class GetItemHandler(ConcurrentDictionary<Guid, ItemDto> store)
    : IQueryHandler<GetItemQuery, ItemDto?>
{
    public ValueTask<ItemDto?> HandleAsync(GetItemQuery query, CancellationToken ct = default)
    {
        store.TryGetValue(query.Id, out ItemDto? item);
        return ValueTask.FromResult(item);
    }
}

public sealed class ListItemsStreamHandler : IStreamQueryHandler<ListItemsStreamQuery, ItemDto>
{
    public async IAsyncEnumerable<ItemDto> HandleAsync(
        ListItemsStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 0; i < query.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new ItemDto(Guid.NewGuid(), $"Item-{i}");
            await Task.Yield();
        }
    }
}

public sealed class GetProductPriceHandler : IQueryHandler<GetProductPriceQuery, decimal>
{
    public int InvocationCount { get; private set; }

    public ValueTask<decimal> HandleAsync(GetProductPriceQuery query, CancellationToken ct = default)
    {
        InvocationCount++;
        return ValueTask.FromResult(query.ProductName switch
        {
            "Widget" => 29.99m,
            "Gadget" => 49.99m,
            _ => 9.99m
        });
    }
}

public sealed class ListPricesStreamHandler : IStreamQueryHandler<ListPricesStreamQuery, decimal>
{
    public async IAsyncEnumerable<decimal> HandleAsync(
        ListPricesStreamQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        decimal[] prices = [5.00m, 15.00m, 25.00m, 35.00m, 50.00m, 75.00m, 100.00m];
        foreach (var price in prices)
        {
            yield return price;
            await Task.Yield();
        }
    }
}

// --- Notification Handlers ---

public sealed class ItemCreatedHandler1(List<string> log)
    : INotificationHandler<ItemCreatedNotification>
{
    public async ValueTask HandleAsync(ItemCreatedNotification notification, CancellationToken ct = default)
    {
        await Task.Delay(100, ct).ConfigureAwait(false);
        log.Add($"Handler1:{notification.Name}");
    }
}

public sealed class ItemCreatedHandler2(List<string> log)
    : INotificationHandler<ItemCreatedNotification>
{
    public async ValueTask HandleAsync(ItemCreatedNotification notification, CancellationToken ct = default)
    {
        await Task.Delay(100, ct).ConfigureAwait(false);
        log.Add($"Handler2:{notification.Name}");
    }
}

public sealed class ItemCreatedHandler3(List<string> log)
    : INotificationHandler<ItemCreatedNotification>
{
    public async ValueTask HandleAsync(ItemCreatedNotification notification, CancellationToken ct = default)
    {
        await Task.Delay(100, ct).ConfigureAwait(false);
        log.Add($"Handler3:{notification.Name}");
    }
}

public sealed class FaultyNotificationHandler1(List<string> log)
    : INotificationHandler<FaultyNotification>
{
    public async ValueTask HandleAsync(FaultyNotification notification, CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        log.Add("FaultyHandler1:ok");
    }
}

public sealed class FaultyNotificationHandler2(List<string> log)
    : INotificationHandler<FaultyNotification>
{
    public ValueTask HandleAsync(FaultyNotification notification, CancellationToken ct = default)
    {
        log.Add("FaultyHandler2:throwing");
        throw new InvalidOperationException($"FaultyHandler2:{notification.Message}");
    }
}

public sealed class FaultyNotificationHandler3(List<string> log)
    : INotificationHandler<FaultyNotification>
{
    public async ValueTask HandleAsync(FaultyNotification notification, CancellationToken ct = default)
    {
        await Task.Delay(50, ct).ConfigureAwait(false);
        log.Add("FaultyHandler3:ok");
    }
}
