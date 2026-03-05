namespace Messaggero.Transport.Kafka;

/// <summary>
/// Configuration for the Kafka transport.
/// </summary>
public sealed class KafkaConfiguration
{
    /// <summary>
    /// Comma-separated list of broker addresses. Required.
    /// </summary>
    public string BootstrapServers { get; set; } = string.Empty;

    /// <summary>
    /// Producer acknowledgment level. Default is "all".
    /// </summary>
    public string Acks { get; set; } = "all";

    /// <summary>
    /// Whether to enable idempotent producer. Default is true.
    /// </summary>
    public bool EnableIdempotence { get; set; } = true;

    /// <summary>
    /// Compression codec. Default is "lz4".
    /// </summary>
    public string CompressionType { get; set; } = "lz4";

    /// <summary>
    /// Producer batch size in bytes. Default is 131072 (128 KB).
    /// </summary>
    public int BatchSize { get; set; } = 131072;

    /// <summary>
    /// Batch accumulation delay in milliseconds. Default is 10.
    /// </summary>
    public int LingerMs { get; set; } = 10;

    /// <summary>
    /// Consumer group session timeout in milliseconds. Default is 45000.
    /// </summary>
    public int SessionTimeoutMs { get; set; } = 45000;

    /// <summary>
    /// Consumer heartbeat interval in milliseconds. Default is 3000.
    /// </summary>
    public int HeartbeatIntervalMs { get; set; } = 3000;

    /// <summary>
    /// Security protocol. Default is "Plaintext".
    /// </summary>
    public string SecurityProtocol { get; set; } = "Plaintext";

    /// <summary>
    /// Validates the configuration and throws if required fields are missing or invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BootstrapServers))
            throw new ArgumentException("BootstrapServers is required for the Kafka transport.", nameof(BootstrapServers));
        if (BatchSize < 0)
            throw new ArgumentException("BatchSize must be non-negative.", nameof(BatchSize));
        if (LingerMs < 0)
            throw new ArgumentException("LingerMs must be non-negative.", nameof(LingerMs));
    }
}
