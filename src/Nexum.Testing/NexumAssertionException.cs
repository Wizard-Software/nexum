namespace Nexum.Testing;

/// <summary>
/// Exception thrown when a Nexum testing assertion fails.
/// </summary>
public sealed class NexumAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="NexumAssertionException"/> with the specified message.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    public NexumAssertionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="NexumAssertionException"/> with the specified message
    /// and inner exception.
    /// </summary>
    /// <param name="message">The assertion failure message.</param>
    /// <param name="innerException">The exception that caused this assertion failure.</param>
    public NexumAssertionException(string message, Exception innerException) : base(message, innerException) { }
}
