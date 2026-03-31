using Messaggero.Model;

namespace Messaggero.Configuration;

/// <summary>
/// Per-transport configuration for message processing retry behavior.
/// </summary>
public sealed class RetryPolicyOptions
{
    /// <summary>Maximum retry attempts (including first attempt). Default: 3.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Backoff strategy. Default: Exponential.</summary>
    public BackoffStrategy BackoffStrategy { get; set; } = BackoffStrategy.Exponential;

    /// <summary>Delay before first retry. Default: 1 second.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Cap on backoff delay (for exponential). Default: 30 seconds.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Predicate to filter which exceptions trigger retry. Null = all exceptions.</summary>
    public Func<Exception, bool>? RetryableExceptions { get; set; }

    /// <summary>Where to route messages after retries exhausted.</summary>
    public Destination? DeadLetterDestination { get; set; }
}
