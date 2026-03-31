namespace Messaggero.Configuration;

/// <summary>
/// Internal model binding a handler type to message type and configuration.
/// </summary>
public sealed class HandlerRegistration
{
    /// <summary>Message type this handler processes.</summary>
    public required string MessageType { get; init; }

    /// <summary>CLR type implementing IMessageHandler&lt;T&gt;.</summary>
    public required Type HandlerType { get; init; }

    /// <summary>The concrete message CLR type.</summary>
    public required Type MessageClrType { get; init; }

    /// <summary>If set, handler receives messages from this transport only.</summary>
    public string? TransportScope { get; init; }

    /// <summary>Max concurrent handler invocations.</summary>
    public int MaxConcurrency { get; init; } = 1;
}
