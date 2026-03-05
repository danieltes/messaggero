namespace Messaggero.Abstractions;

/// <summary>
/// Result of a health check query on the active transport connection.
/// </summary>
public sealed record HealthCheckResult
{
    /// <summary>
    /// Whether the transport connection is currently healthy.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Name of the transport being checked.
    /// </summary>
    public required string TransportName { get; init; }

    /// <summary>
    /// Human-readable description of the health status.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Additional health check data.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Data { get; init; }

    /// <summary>
    /// Per-transport health entries for multi-transport configurations.
    /// </summary>
    public IReadOnlyList<TransportHealthEntry> TransportEntries { get; init; } = [];
}
