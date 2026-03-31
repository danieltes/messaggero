namespace Messaggero.Errors;

/// <summary>
/// Base exception for all Messaggero errors.
/// </summary>
public class MessagingException : Exception
{
    /// <inheritdoc />
    public MessagingException(string message) : base(message) { }
    /// <inheritdoc />
    public MessagingException(string message, Exception innerException) : base(message, innerException) { }
}
