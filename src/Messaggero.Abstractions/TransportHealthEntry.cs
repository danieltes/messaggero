namespace Messaggero.Abstractions;

/// <summary>
/// Health status for a single registered transport.
/// </summary>
public sealed class TransportHealthEntry
{
    /// <summary>
    /// Name of the transport being reported on.
    /// </summary>
    public required string TransportName { get; init; }

    /// <summary>
    /// Whether the transport connection is currently healthy.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Human-readable description of the transport health status.
    /// </summary>
    public string? Description { get; init; }
}
