namespace Messaggero.Abstractions;

/// <summary>
/// Policy applied when a message handler fails during processing.
/// Use the static factory methods to create instances.
/// </summary>
public sealed record ErrorStrategy
{
    /// <summary>
    /// The type of error handling strategy.
    /// </summary>
    public ErrorStrategyType Type { get; init; }

    /// <summary>
    /// Maximum number of retry attempts. Used when <see cref="Type"/> is <see cref="ErrorStrategyType.Retry"/>.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Initial delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Multiplier applied to the retry delay for exponential backoff.
    /// </summary>
    public double RetryBackoffMultiplier { get; init; } = 2.0;

    /// <summary>
    /// Destination for dead-lettered messages. Used when <see cref="Type"/> is <see cref="ErrorStrategyType.DeadLetter"/>.
    /// </summary>
    public string? DeadLetterDestination { get; init; }

    /// <summary>
    /// Creates a retry error strategy with exponential backoff.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retry attempts.</param>
    /// <param name="delay">Initial delay between retries. Defaults to 1 second.</param>
    /// <param name="multiplier">Backoff multiplier. Defaults to 2.0.</param>
    /// <returns>A retry error strategy.</returns>
    public static ErrorStrategy Retry(
        int maxRetries = 3,
        TimeSpan? delay = null,
        double multiplier = 2.0)
        => new()
        {
            Type = ErrorStrategyType.Retry,
            MaxRetries = maxRetries,
            RetryDelay = delay ?? TimeSpan.FromSeconds(1),
            RetryBackoffMultiplier = multiplier
        };

    /// <summary>
    /// Creates a dead-letter error strategy that forwards failed messages to a designated destination.
    /// </summary>
    /// <param name="destination">The destination to forward dead-lettered messages to.</param>
    /// <returns>A dead-letter error strategy.</returns>
    public static ErrorStrategy DeadLetter(string destination)
        => new() { Type = ErrorStrategyType.DeadLetter, DeadLetterDestination = destination };

    /// <summary>
    /// Creates a reject error strategy that discards and logs failed messages.
    /// </summary>
    /// <returns>A reject error strategy.</returns>
    public static ErrorStrategy Reject()
        => new() { Type = ErrorStrategyType.Reject };
}

/// <summary>
/// The type of error handling strategy.
/// </summary>
public enum ErrorStrategyType
{
    /// <summary>Retry the message handler with exponential backoff.</summary>
    Retry,

    /// <summary>Forward the message to a dead-letter destination.</summary>
    DeadLetter,

    /// <summary>Reject the message (no requeue) and log the failure.</summary>
    Reject
}
