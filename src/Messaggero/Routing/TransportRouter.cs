using System.Collections.Concurrent;
using Messaggero.Abstractions;

namespace Messaggero.Routing;

/// <summary>
/// Routes messages to the appropriate transport based on destination patterns,
/// message type hierarchy, and default transport configuration.
/// </summary>
internal sealed class TransportRouter : ITransportRouter
{
    private readonly IReadOnlyList<RoutingRule> _destinationRules;
    private readonly IReadOnlyList<RoutingRule> _typeRules;
    private readonly string? _defaultTransportName;
    private readonly ConcurrentDictionary<Type, string?> _typeCache = new();

    public IReadOnlyDictionary<string, IMessageBusTransport> Transports { get; }

    /// <summary>
    /// Creates a new router and validates all rules at startup.
    /// </summary>
    /// <param name="transports">Registered transports keyed by name.</param>
    /// <param name="destinationRules">Destination-based routing rules.</param>
    /// <param name="typeRules">Type-based routing rules.</param>
    /// <param name="defaultTransportName">Optional default transport name.</param>
    /// <exception cref="InvalidOperationException">Validation failure (conflicts, unregistered references).</exception>
    public TransportRouter(
        IReadOnlyDictionary<string, IMessageBusTransport> transports,
        IReadOnlyList<RoutingRule> destinationRules,
        IReadOnlyList<RoutingRule> typeRules,
        string? defaultTransportName)
    {
        ArgumentNullException.ThrowIfNull(transports);
        ArgumentNullException.ThrowIfNull(destinationRules);
        ArgumentNullException.ThrowIfNull(typeRules);

        if (transports.Count == 0)
            throw new InvalidOperationException("At least one transport must be registered.");

        Transports = transports;
        _defaultTransportName = defaultTransportName;

        Validate(transports, destinationRules, typeRules, defaultTransportName);

        // Sort destination rules by specificity (most specific first)
        _destinationRules = destinationRules
            .OrderBy(r => r.DestinationPattern!.Specificity)
            .ToList();

        _typeRules = typeRules;
    }

    /// <inheritdoc />
    public IMessageBusTransport ResolveTransport(string destination, Type? messageType = null)
    {
        // 1. Evaluate destination rules (sorted by specificity)
        foreach (var rule in _destinationRules)
        {
            if (rule.DestinationPattern!.IsMatch(destination))
            {
                return Transports[rule.TransportName];
            }
        }

        // 2. Evaluate type rules (class hierarchy walk)
        if (messageType is not null)
        {
            var transportName = _typeCache.GetOrAdd(messageType, ResolveTypeTransportName);
            if (transportName is not null)
            {
                return Transports[transportName];
            }
        }

        // 3. Default transport
        if (_defaultTransportName is not null)
        {
            return Transports[_defaultTransportName];
        }

        // 4. Implicit default: single transport with no rules
        if (Transports.Count == 1)
        {
            return Transports.Values.First();
        }

        throw new InvalidOperationException(
            $"Cannot route message to destination '{destination}': no matching routing rule and no default transport configured.");
    }

    private string? ResolveTypeTransportName(Type type)
    {
        // Walk the class hierarchy from exact type up to (but not including) object
        var current = type;
        while (current is not null && current != typeof(object))
        {
            foreach (var rule in _typeRules)
            {
                if (rule.MessageType == current)
                {
                    return rule.TransportName;
                }
            }

            current = current.BaseType;
        }

        return null;
    }

    private static void Validate(
        IReadOnlyDictionary<string, IMessageBusTransport> transports,
        IReadOnlyList<RoutingRule> destinationRules,
        IReadOnlyList<RoutingRule> typeRules,
        string? defaultTransportName)
    {
        // Validate default transport exists
        if (defaultTransportName is not null && !transports.ContainsKey(defaultTransportName))
        {
            var registered = string.Join(", ", transports.Keys.Select(k => $"'{k}'"));
            throw new InvalidOperationException(
                $"Default transport '{defaultTransportName}' is not registered. Registered transports: {registered}.");
        }

        // Validate all destination rules reference registered transports + check for conflicts
        var destinationPatterns = new Dictionary<string, string>();
        foreach (var rule in destinationRules)
        {
            if (!transports.ContainsKey(rule.TransportName))
            {
                var registered = string.Join(", ", transports.Keys.Select(k => $"'{k}'"));
                throw new InvalidOperationException(
                    $"Routing rule references transport '{rule.TransportName}' which is not registered. Registered transports: {registered}.");
            }

            var pattern = rule.DestinationPattern!.RawPattern;
            if (destinationPatterns.TryGetValue(pattern, out var existing))
            {
                if (!string.Equals(existing, rule.TransportName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Conflicting routing rules: destination pattern '{pattern}' is mapped to both '{existing}' and '{rule.TransportName}'.");
                }
            }
            else
            {
                destinationPatterns[pattern] = rule.TransportName;
            }
        }

        // Validate all type rules reference registered transports + check for conflicts
        var typeMapping = new Dictionary<Type, string>();
        foreach (var rule in typeRules)
        {
            if (!transports.ContainsKey(rule.TransportName))
            {
                var registered = string.Join(", ", transports.Keys.Select(k => $"'{k}'"));
                throw new InvalidOperationException(
                    $"Routing rule references transport '{rule.TransportName}' which is not registered. Registered transports: {registered}.");
            }

            if (typeMapping.TryGetValue(rule.MessageType!, out var existing))
            {
                if (!string.Equals(existing, rule.TransportName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Conflicting routing rules: message type '{rule.MessageType!.FullName}' is mapped to both '{existing}' and '{rule.TransportName}'.");
                }
            }
            else
            {
                typeMapping[rule.MessageType!] = rule.TransportName;
            }
        }
    }
}
