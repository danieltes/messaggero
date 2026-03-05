namespace Messaggero.Abstractions;

/// <summary>
/// Configuration for a single subscription.
/// </summary>
public sealed record SubscriptionOptions
{
    /// <summary>
    /// Maximum number of messages processed concurrently for this subscription.
    /// Default is 1 (sequential processing). Per-key ordering is preserved regardless.
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Error-handling policy applied when a message handler fails.
    /// </summary>
    public ErrorStrategy ErrorStrategy { get; init; } = ErrorStrategy.Retry();

    /// <summary>
    /// Broker-level prefetch count. Defaults to <see cref="MaxConcurrency"/> if not set.
    /// Must be greater than or equal to <see cref="MaxConcurrency"/>.
    /// </summary>
    public int? PrefetchCount { get; init; }
}
