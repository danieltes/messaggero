namespace Messaggero.Abstractions;

/// <summary>
/// Structured notification emitted by the library at key moments for observability.
/// </summary>
public sealed record LifecycleEvent
{
    /// <summary>
    /// The type of lifecycle event.
    /// </summary>
    public required LifecycleEventType EventType { get; init; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Name of the transport that emitted the event.
    /// </summary>
    public required string TransportName { get; init; }

    /// <summary>
    /// The relevant destination, if applicable.
    /// </summary>
    public string? Destination { get; init; }

    /// <summary>
    /// The relevant message ID, if applicable.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Error details, if applicable.
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// Additional context metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Types of lifecycle events emitted by the messaging library.
/// </summary>
public enum LifecycleEventType
{
    /// <summary>Transport successfully connected to the broker.</summary>
    TransportConnected,

    /// <summary>Transport disconnected from the broker.</summary>
    TransportDisconnected,

    /// <summary>Transport is attempting to reconnect to the broker.</summary>
    TransportReconnecting,

    /// <summary>Transport has exhausted all reconnection attempts.</summary>
    TransportFailed,

    /// <summary>A message was successfully published.</summary>
    MessagePublished,

    /// <summary>A message was received from the broker.</summary>
    MessageReceived,

    /// <summary>An error occurred during message processing.</summary>
    MessageError
}
