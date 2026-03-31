namespace Messaggero.Errors;

/// <summary>
/// Handler processing failed after all retry attempts. Message routed to dead-letter.
/// </summary>
public class RetryExhaustedException : MessagingException
{
    /// <summary>Total number of attempts before exhaustion.</summary>
    public int Attempts { get; }

    public RetryExhaustedException(int attempts, string message)
        : base(message)
    {
        Attempts = attempts;
    }

    public RetryExhaustedException(int attempts, string message, Exception innerException)
        : base(message, innerException)
    {
        Attempts = attempts;
    }
}
