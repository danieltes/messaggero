namespace Messaggero.Errors;

/// <summary>
/// Payload deserialization failed. Message routed to dead-letter.
/// </summary>
public class DeserializationException : MessagingException
{
    public DeserializationException(string message) : base(message) { }
    public DeserializationException(string message, Exception innerException) : base(message, innerException) { }
}
