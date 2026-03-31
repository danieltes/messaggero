using Messaggero.Errors;

namespace Messaggero.Routing;

/// <summary>
/// Keyed collection of <see cref="RoutingRule"/>s consulted at publish time.
/// Immutable after build. Dictionary-based O(1) lookup by message type.
/// </summary>
public sealed class RoutingTable
{
    private readonly Dictionary<string, RoutingRule> _rules;

    public RoutingTable(IEnumerable<RoutingRule> rules)
    {
        _rules = new Dictionary<string, RoutingRule>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            _rules[rule.MessageType] = rule;
        }
    }

    /// <summary>All rules keyed by message type.</summary>
    public IReadOnlyDictionary<string, RoutingRule> Rules => _rules;

    /// <summary>
    /// Resolves the routing rule for a message type.
    /// Throws <see cref="NoRouteFoundException"/> if no rule matches.
    /// </summary>
    public RoutingRule Resolve(string messageType)
    {
        if (_rules.TryGetValue(messageType, out var rule))
            return rule;

        throw new NoRouteFoundException(messageType);
    }
}
