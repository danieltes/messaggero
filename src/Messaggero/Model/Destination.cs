namespace Messaggero.Model;

/// <summary>
/// Broker-agnostic reference to a publish/subscribe target.
/// </summary>
public sealed class Destination
{
    /// <summary>Logical destination name (e.g., "orders", "emails").</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Adapter-specific overrides (e.g., Kafka partition key, RabbitMQ routing key).
    /// </summary>
    public IReadOnlyDictionary<string, string>? TransportOverrides { get; init; }
}
