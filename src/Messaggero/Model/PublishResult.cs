using Messaggero.Errors;

namespace Messaggero.Model;

/// <summary>
/// Typed outcome of a publish operation.
/// </summary>
public sealed class PublishResult
{
    /// <summary>The published message's ID.</summary>
    public required string MessageId { get; init; }

    /// <summary>Per-transport results (one per target in fan-out).</summary>
    public required IReadOnlyList<TransportOutcome> Outcomes { get; init; }

    /// <summary>True if ALL transport outcomes succeeded.</summary>
    public bool IsSuccess => Outcomes.All(o => o.Success);
}

/// <summary>
/// Per-transport result within a <see cref="PublishResult"/>.
/// </summary>
public sealed class TransportOutcome
{
    /// <summary>Named transport that handled this outcome.</summary>
    public required string TransportName { get; init; }

    /// <summary>Whether delivery to this transport succeeded.</summary>
    public required bool Success { get; init; }

    /// <summary>Adapter-specific ack metadata (e.g., Kafka offset/partition).</summary>
    public IReadOnlyDictionary<string, string>? BrokerMetadata { get; init; }

    /// <summary>Populated on failure.</summary>
    public PublishFailure? Error { get; init; }
}
