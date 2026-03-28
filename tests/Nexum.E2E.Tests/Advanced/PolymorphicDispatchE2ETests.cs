using Microsoft.Extensions.DependencyInjection;
using Nexum.Abstractions;
using Nexum.E2E.Tests.Fixtures;

namespace Nexum.E2E.Tests.Advanced;

[Trait("Category", "E2E")]
public sealed class PolymorphicDispatchE2ETests : IDisposable
{
    private readonly Microsoft.Extensions.Hosting.IHost _host;

    public PolymorphicDispatchE2ETests()
    {
        _host = NexumTestHost.CreateHost();
    }

    public void Dispose() => _host.Dispose();

    // E2E-060: Different concrete subtypes of BasePaymentCommand dispatch to their specific handlers
    [Fact]
    public async Task DispatchAsync_CreditCardPaymentCommand_RoutesToCreditCardHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new CreditCardPaymentCommand(149.99m), ct);

        result.Should().Be("CreditCard:149.99");
    }

    [Fact]
    public async Task DispatchAsync_BankTransferPaymentCommand_RoutesToBankTransferHandler()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.DispatchAsync(new BankTransferPaymentCommand(2500.00m), ct);

        result.Should().Be("BankTransfer:2500.00");
    }

    // E2E-061: Dispatching same concrete command type twice — second call hits handler resolution cache
    [Fact]
    public async Task DispatchAsync_SameCommandTypeTwice_BothCallsReturnIdenticalResult()
    {
        var ct = TestContext.Current.CancellationToken;
        var dispatcher = _host.Services.GetRequiredService<ICommandDispatcher>();

        var command = new CreditCardPaymentCommand(149.99m);

        var result1 = await dispatcher.DispatchAsync(command, ct);
        var result2 = await dispatcher.DispatchAsync(command, ct);

        result1.Should().Be("CreditCard:149.99");
        result2.Should().Be(result1);
    }
}
