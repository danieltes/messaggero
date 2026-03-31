namespace Messaggero.Errors;

/// <summary>
/// Broker rejected or was unreachable during publish.
/// Used both as a thrown exception and as TransportOutcome.Error data.
/// </summary>
public class PublishFailure : MessagingException
{
    /// <summary>Name of the transport that failed.</summary>
    public string TransportName { get; }
    /// <summary>Optional broker-level error string.</summary>
    public string? BrokerError { get; }

    public PublishFailure(string transportName, string message, string? brokerError = null)
        : base(message)
    {
        TransportName = transportName;
        BrokerError = brokerError;
    }

    public PublishFailure(string transportName, string message, Exception innerException)
        : base(message, innerException)
    {
        TransportName = transportName;
    }
}
