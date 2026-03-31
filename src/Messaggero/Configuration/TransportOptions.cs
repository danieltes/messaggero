namespace Messaggero.Configuration;

/// <summary>
/// Base transport options.
/// </summary>
public class TransportOptions
{
    /// <summary>Retry policy for this transport.</summary>
    public RetryPolicyOptions RetryPolicy { get; set; } = new();

    /// <summary>Prefetch/buffer limit controlling the maximum number of unprocessed messages held in memory.</summary>
    public int PrefetchCount { get; set; } = 100;
}
