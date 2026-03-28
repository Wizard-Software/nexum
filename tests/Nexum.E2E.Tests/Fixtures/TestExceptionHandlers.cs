using Nexum.Abstractions;

namespace Nexum.E2E.Tests.Fixtures;

public sealed class FailingCommandExceptionHandler(List<string> exceptionLog)
    : ICommandExceptionHandler<FailingCommand, InvalidOperationException>
{
    public ValueTask HandleAsync(
        FailingCommand command,
        InvalidOperationException exception,
        CancellationToken ct = default)
    {
        exceptionLog.Add($"ExceptionHandler:{exception.Message}");
        // Exception handlers must re-throw — side-effects only (Z5)
        throw exception;
    }
}

public sealed class FaultyNotificationExceptionHandler(List<string> exceptionLog)
    : INotificationExceptionHandler<FaultyNotification, InvalidOperationException>
{
    public ValueTask HandleAsync(
        FaultyNotification notification,
        InvalidOperationException exception,
        CancellationToken ct = default)
    {
        exceptionLog.Add($"NotifExHandler:{exception.Message}");
        throw exception;
    }
}
