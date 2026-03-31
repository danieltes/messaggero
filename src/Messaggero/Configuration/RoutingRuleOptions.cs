using Messaggero.Model;

namespace Messaggero.Configuration;

/// <summary>
/// Configuration for a routing rule entry.
/// </summary>
public sealed class RoutingRuleOptions
{
    /// <summary>Named transport registrations to deliver to.</summary>
    public List<string> Transports { get; } = [];

    /// <summary>Override destination; if null, uses default destination for the message type.</summary>
    public Destination? Destination { get; set; }
}
