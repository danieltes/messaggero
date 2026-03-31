using Messaggero.Model;

namespace Messaggero.Routing;

/// <summary>
/// Maps a message type to one or more named transport registrations.
/// </summary>
public sealed class RoutingRule
{
    /// <summary>The message type this rule applies to.</summary>
    public required string MessageType { get; init; }

    /// <summary>Named transport registrations to deliver to. Single = direct, multiple = fan-out.</summary>
    public required IReadOnlyList<string> Transports { get; init; }

    /// <summary>Override destination; if null, uses default destination for the message type.</summary>
    public Destination? Destination { get; init; }
}
