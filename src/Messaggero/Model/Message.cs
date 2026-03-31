namespace Messaggero.Model;

/// <summary>
/// The envelope transmitted through the library for both publishing and consuming.
/// </summary>
public sealed class Message
{
    /// <summary>
    /// Unique identifier. Library-generated (GUID) before routing; caller MAY override.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Message type identifier. Primary key for routing decisions.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Serialized message body (opaque bytes).
    /// </summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }

    /// <summary>
    /// Key-value metadata.
    /// </summary>
    public required MessageHeaders Headers { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Populated on consumed messages only. Name of the transport that delivered the message.
    /// </summary>
    public string? SourceTransport { get; init; }
}
