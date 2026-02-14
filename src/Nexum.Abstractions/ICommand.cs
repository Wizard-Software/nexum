namespace Nexum.Abstractions;

/// <summary>
/// Non-generic marker interface for all commands.
/// Enables exception handler constraints without requiring the result type parameter.
/// </summary>
public interface ICommand;

/// <summary>
/// Represents a command that returns a result of type <typeparamref name="TResult"/>.
/// </summary>
/// <typeparam name="TResult">The type of result produced by the command. Covariant.</typeparam>
public interface ICommand<out TResult> : ICommand;

/// <summary>
/// Represents a command that does not return a meaningful result.
/// Alias for <see cref="ICommand{TResult}"/> with <see cref="Unit"/> as the result type.
/// </summary>
public interface IVoidCommand : ICommand<Unit>;
