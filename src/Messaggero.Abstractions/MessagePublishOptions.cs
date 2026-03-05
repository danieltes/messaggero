namespace Messaggero.Abstractions;

/// <summary>
/// Options for publishing a message.
/// </summary>
public sealed record MessagePublishOptions
{
    /// <summary>
    /// Routing/partition key for message ordering.
    /// </summary>
    public string? RoutingKey { get; init; }

    /// <summary>
    /// Additional headers to include with the message.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Correlation identifier for request-reply patterns and tracing.
    /// </summary>
    public string? CorrelationId { get; init; }
}
