namespace Messaggero.Errors;

/// <summary>
/// Builder validation failed (invalid routing, missing transports, etc.). Thrown at build time.
/// </summary>
public class MessagingConfigurationException : MessagingException
{
    /// <summary>All validation errors detected during build.</summary>
    public IReadOnlyList<string> ValidationErrors { get; }

    public MessagingConfigurationException(string message)
        : base(message)
    {
        ValidationErrors = [message];
    }

    public MessagingConfigurationException(IReadOnlyList<string> errors)
        : base($"Messaging configuration is invalid: {string.Join("; ", errors)}")
    {
        ValidationErrors = errors;
    }
}
