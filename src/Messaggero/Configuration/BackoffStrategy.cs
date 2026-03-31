namespace Messaggero.Configuration;

/// <summary>
/// Backoff strategy for retry policies.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>Constant delay between retries.</summary>
    Fixed,

    /// <summary>Delay doubles each attempt, capped at MaxDelay.</summary>
    Exponential
}
