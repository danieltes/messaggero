namespace Messaggero.Kafka;

/// <summary>
/// Kafka-specific transport options.
/// </summary>
public sealed class KafkaOptions : Configuration.TransportOptions
{
    /// <summary>Kafka bootstrap servers. Falls back to MESSAGGERO_KAFKA_BOOTSTRAP_SERVERS env var.</summary>
    public string BootstrapServers { get; set; } =
        Environment.GetEnvironmentVariable("MESSAGGERO_KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9092";

    /// <summary>Consumer group ID. Falls back to MESSAGGERO_KAFKA_GROUP_ID env var.</summary>
    public string GroupId { get; set; } =
        Environment.GetEnvironmentVariable("MESSAGGERO_KAFKA_GROUP_ID") ?? "messaggero-default";

    /// <summary>Additional producer configuration.</summary>
    public Dictionary<string, string> ProducerConfig { get; set; } = [];

    /// <summary>Additional consumer configuration.</summary>
    public Dictionary<string, string> ConsumerConfig { get; set; } = [];
}
