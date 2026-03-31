using Messaggero.Model;

namespace Messaggero.Abstractions;

/// <summary>
/// Metadata available to handlers during message processing.
/// </summary>
public sealed class MessageContext
{
    /// <summary>Unique message ID.</summary>
    public required string MessageId { get; init; }

    /// <summary>Message type string.</summary>
    public required string MessageType { get; init; }

    /// <summary>Name of the transport that delivered this message.</summary>
    public required string SourceTransport { get; init; }

    /// <summary>Message headers.</summary>
    public required MessageHeaders Headers { get; init; }

    /// <summary>Message timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Current retry attempt (1 = first delivery, 2+ = retry).</summary>
    public required int DeliveryAttempt { get; init; }
}
