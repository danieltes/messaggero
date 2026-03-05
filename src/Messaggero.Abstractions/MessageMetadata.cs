namespace Messaggero.Abstractions;

/// <summary>
/// Wire-level metadata passed between core and transport layer.
/// Not exposed to consumers directly.
/// </summary>
public sealed record MessageMetadata
{
    /// <summary>
    /// Unique message identifier.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// Routing/partition key for ordering.
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// Key-value metadata headers.
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();

    /// <summary>
    /// When the message was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Serialization content type (e.g., "application/json").
    /// </summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>
    /// For request-reply and tracing correlation.
    /// </summary>
    public string? CorrelationId { get; init; }
}
