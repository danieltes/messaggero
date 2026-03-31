namespace Messaggero.Errors;

/// <summary>
/// A routing rule or handler scope references a transport name not registered. Thrown at build time.
/// </summary>
public class TransportNotFoundException : MessagingException
{
    /// <summary>Name of the unregistered transport.</summary>
    public string TransportName { get; }

    public TransportNotFoundException(string transportName)
        : base($"Transport '{transportName}' is not registered.")
    {
        TransportName = transportName;
    }
}
