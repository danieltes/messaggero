namespace Messaggero.Errors;

/// <summary>
/// A transport's connection was lost at runtime.
/// Emitted as event; does not stop other transports.
/// </summary>
public class TransportDegradedException : MessagingException
{
    /// <summary>Name of the degraded transport.</summary>
    public string TransportName { get; }

    public TransportDegradedException(string transportName, string message)
        : base(message)
    {
        TransportName = transportName;
    }

    public TransportDegradedException(string transportName, string message, Exception innerException)
        : base(message, innerException)
    {
        TransportName = transportName;
    }
}
