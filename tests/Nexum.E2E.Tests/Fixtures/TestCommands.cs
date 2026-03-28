using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

// Basic command with result
public sealed record CreateItemCommand(string Name) : ICommand<Guid>;

// Void command
public sealed record DeleteItemCommand(Guid Id) : IVoidCommand;

// Command for depth guard testing
public sealed record RecursiveCommand(bool ShouldRecurse = true) : ICommand<string>;

// Command for exception handling testing
public sealed record FailingCommand(string Message) : ICommand<string>;

// Commands for lifetime testing
public sealed record TransientTestCommand : ICommand<Guid>;
public sealed record ScopedTestCommand : ICommand<Guid>;
public sealed record SingletonTestCommand : ICommand<Guid>;

// Commands for polymorphic testing
public abstract record BasePaymentCommand(decimal Amount) : ICommand<string>;
public sealed record CreditCardPaymentCommand(decimal Amount) : BasePaymentCommand(Amount);
public sealed record BankTransferPaymentCommand(decimal Amount) : BasePaymentCommand(Amount);

// Command for cancellation testing
public sealed record SlowCommand(int DelayMs = 2000) : ICommand<string>;

// Command for behavior pipeline testing
public sealed record TrackedCommand(string Input) : ICommand<string>;

// Command for unregistered handler test
public sealed record UnregisteredCommand : ICommand<string>;
