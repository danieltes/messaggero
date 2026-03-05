namespace Messaggero.Abstractions;

/// <summary>
/// Configuration for exponential backoff reconnection behavior.
/// </summary>
public sealed record ReconnectionOptions
{
    /// <summary>
    /// Initial delay before the first reconnection attempt. Must be greater than zero.
    /// </summary>
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Multiplier applied to the delay after each failed reconnection attempt. Must be ≥ 1.0.
    /// </summary>
    public double Multiplier { get; init; } = 2.0;

    /// <summary>
    /// Maximum delay between reconnection attempts. Must be ≥ <see cref="InitialDelay"/>.
    /// </summary>
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of reconnection attempts. 0 means unlimited.
    /// </summary>
    public int MaxAttempts { get; init; } = 10;

    /// <summary>
    /// Validates the reconnection options and throws if any values are invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (InitialDelay <= TimeSpan.Zero)
            throw new ArgumentException("InitialDelay must be greater than zero.", nameof(InitialDelay));
        if (Multiplier < 1.0)
            throw new ArgumentException("Multiplier must be greater than or equal to 1.0.", nameof(Multiplier));
        if (MaxDelay < InitialDelay)
            throw new ArgumentException("MaxDelay must be greater than or equal to InitialDelay.", nameof(MaxDelay));
        if (MaxAttempts < 0)
            throw new ArgumentException("MaxAttempts must be greater than or equal to 0.", nameof(MaxAttempts));
    }
}
