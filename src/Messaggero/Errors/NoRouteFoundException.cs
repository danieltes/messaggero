namespace Messaggero.Errors;

/// <summary>
/// No routing rule matches the published message type.
/// </summary>
public class NoRouteFoundException : MessagingException
{
    /// <summary>The message type for which no route was found.</summary>
    public string MessageType { get; }

    public NoRouteFoundException(string messageType)
        : base($"No routing rule found for message type '{messageType}'.")
    {
        MessageType = messageType;
    }
}
