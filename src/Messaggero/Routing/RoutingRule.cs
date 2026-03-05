namespace Messaggero.Routing;

/// <summary>
/// A single routing rule mapping a destination pattern or message type to a named transport.
/// Exactly one of <see cref="DestinationPattern"/> or <see cref="MessageType"/> must be non-null.
/// </summary>
internal sealed class RoutingRule
{
    /// <summary>
    /// Name of the target transport (must match a registered transport).
    /// </summary>
    public string TransportName { get; }

    /// <summary>
    /// Compiled glob pattern for destination matching. Null for type-based rules.
    /// </summary>
    public DestinationPattern? DestinationPattern { get; }

    /// <summary>
    /// CLR type for type-based matching. Null for destination-based rules.
    /// </summary>
    public Type? MessageType { get; }

    private RoutingRule(string transportName, DestinationPattern? destinationPattern, Type? messageType)
    {
        TransportName = transportName;
        DestinationPattern = destinationPattern;
        MessageType = messageType;
    }

    /// <summary>
    /// Creates a destination-based routing rule.
    /// </summary>
    public static RoutingRule ForDestination(string transportName, string destinationPattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        return new RoutingRule(transportName, new DestinationPattern(destinationPattern), null);
    }

    /// <summary>
    /// Creates a type-based routing rule.
    /// </summary>
    public static RoutingRule ForType(string transportName, Type messageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        ArgumentNullException.ThrowIfNull(messageType);
        return new RoutingRule(transportName, null, messageType);
    }
}
