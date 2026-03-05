namespace Messaggero.Abstractions;

/// <summary>
/// Transport-agnostic container for a single message delivered to handlers.
/// </summary>
/// <typeparam name="T">The type of the message payload.</typeparam>
public sealed record MessageEnvelope<T>
{
    /// <summary>
    /// Unique identifier for the message (UUID v7 recommended for time-ordering).
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Deserialized message body.
    /// </summary>
    public required T Payload { get; init; }

    /// <summary>
    /// Logical destination name (topic/queue).
    /// </summary>
    public required string Destination { get; init; }

    /// <summary>
    /// Key-value metadata headers (may be empty).
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// Routing/partition key; determines ordering scope. Null means no ordering guarantee.
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Serialization format (e.g., "application/json").
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// For request-reply and tracing correlation.
    /// </summary>
    public string? CorrelationId { get; init; }
}
