namespace Messaggero.Configuration;

/// <summary>
/// Configuration for handler registration.
/// </summary>
public sealed class HandlerOptions
{
    /// <summary>Max concurrent handler invocations. Default: 1 (sequential).</summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>If set, handler receives messages from this transport only.</summary>
    public string? TransportScope { get; set; }
}
