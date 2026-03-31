using Messaggero.Abstractions;
using Messaggero.Errors;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;

namespace Messaggero.Configuration;

/// <summary>
/// Fluent builder for configuring the messaging infrastructure.
/// </summary>
public sealed class MessagingBuilder
{
    private readonly Dictionary<string, TransportRegistration> _transports = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string MessageType, RoutingRuleOptions Options)> _routingRules = [];
    private readonly List<HandlerRegistration> _handlers = [];
    private IMessageSerializer _defaultSerializer = new JsonMessageSerializer();
    private bool _observabilityEnabled;

    /// <summary>Registers a named transport adapter.</summary>
    public MessagingBuilder AddTransport(
        string name,
        Func<IServiceProvider, ITransportAdapter> factory,
        Action<TransportOptions>? configure = null)
    {
        var options = new TransportOptions();
        configure?.Invoke(options);

        _transports[name] = new TransportRegistration
        {
            Name = name,
            AdapterFactory = factory,
            Options = options
        };

        return this;
    }

    /// <summary>Registers a named transport adapter with a custom serializer.</summary>
    public MessagingBuilder AddTransport(
        string name,
        Func<IServiceProvider, ITransportAdapter> factory,
        IMessageSerializer serializer,
        Action<TransportOptions>? configure = null)
    {
        var options = new TransportOptions();
        configure?.Invoke(options);

        _transports[name] = new TransportRegistration
        {
            Name = name,
            AdapterFactory = factory,
            Options = options,
            Serializer = serializer
        };

        return this;
    }

    /// <summary>Defines a routing rule for a message type.</summary>
    public MessagingBuilder Route<TMessage>(Action<RoutingRuleBuilder> configure)
        where TMessage : class
    {
        var builder = new RoutingRuleBuilder();
        configure(builder);

        var options = new RoutingRuleOptions { Destination = builder.DestinationValue };
        foreach (var t in builder.TransportNames)
            options.Transports.Add(t);

        _routingRules.Add((typeof(TMessage).Name, options));
        return this;
    }

    /// <summary>Registers a message handler.</summary>
    public MessagingBuilder RegisterHandler<THandler, TMessage>(Action<HandlerOptions>? configure = null)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class
    {
        var opts = new HandlerOptions();
        configure?.Invoke(opts);

        _handlers.Add(new HandlerRegistration
        {
            MessageType = typeof(TMessage).Name,
            HandlerType = typeof(THandler),
            MessageClrType = typeof(TMessage),
            TransportScope = opts.TransportScope,
            MaxConcurrency = opts.MaxConcurrency
        });

        return this;
    }

    /// <summary>Enables structured observability (logs, metrics, traces).</summary>
    public MessagingBuilder EnableObservability()
    {
        _observabilityEnabled = true;
        return this;
    }

    /// <summary>Overrides the default serializer.</summary>
    public MessagingBuilder UseDefaultSerializer(IMessageSerializer serializer)
    {
        _defaultSerializer = serializer;
        return this;
    }

    /// <summary>
    /// Validates configuration and builds the immutable configuration.
    /// Throws <see cref="MessagingConfigurationException"/> on validation failure.
    /// </summary>
    public MessagingConfiguration Build()
    {
        var errors = new List<string>();

        // Validate routing rules reference registered transports
        var routingRules = new List<RoutingRule>();
        var seenMessageTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (messageType, options) in _routingRules)
        {
            if (!seenMessageTypes.Add(messageType))
            {
                errors.Add($"Duplicate routing rule for message type '{messageType}'.");
                continue;
            }

            if (options.Transports.Count == 0)
            {
                errors.Add($"Routing rule for '{messageType}' has no transports specified.");
                continue;
            }

            foreach (var transportName in options.Transports)
            {
                if (!_transports.ContainsKey(transportName))
                {
                    errors.Add($"Routing rule for '{messageType}' references unregistered transport '{transportName}'.");
                }
            }

            routingRules.Add(new RoutingRule
            {
                MessageType = messageType,
                Transports = options.Transports.ToList().AsReadOnly(),
                Destination = options.Destination ?? new Destination { Name = messageType.ToLowerInvariant() }
            });
        }

        // Validate handler transport scopes
        foreach (var handler in _handlers)
        {
            if (handler.TransportScope is not null && !_transports.ContainsKey(handler.TransportScope))
            {
                throw new TransportNotFoundException(handler.TransportScope);
            }
        }

        // Warn on handlers with no matching routing rule (non-fatal)
        foreach (var handler in _handlers)
        {
            if (!_routingRules.Any(r => r.MessageType == handler.MessageType))
            {
                // Handler registered for a message type with no routing rule — allowed but won't receive messages
            }
        }

        if (errors.Count > 0)
            throw new MessagingConfigurationException(errors);

        return new MessagingConfiguration
        {
            Transports = _transports.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase).AsReadOnly(),
            RoutingTable = new RoutingTable(routingRules),
            Handlers = _handlers.AsReadOnly(),
            DefaultSerializer = _defaultSerializer,
            ObservabilityEnabled = _observabilityEnabled
        };
    }
}

/// <summary>
/// Builder for configuring individual routing rules.
/// </summary>
public sealed class RoutingRuleBuilder
{
    internal List<string> TransportNames { get; } = [];
    internal Destination? DestinationValue { get; private set; }

    /// <summary>Routes to a named transport.</summary>
    public RoutingRuleBuilder ToTransport(string transportName)
    {
        TransportNames.Add(transportName);
        return this;
    }

    /// <summary>Overrides the destination.</summary>
    public RoutingRuleBuilder ToDestination(string destinationName)
    {
        DestinationValue = new Destination { Name = destinationName };
        return this;
    }

    /// <summary>Overrides the destination with transport overrides.</summary>
    public RoutingRuleBuilder ToDestination(string destinationName, IReadOnlyDictionary<string, string> overrides)
    {
        DestinationValue = new Destination { Name = destinationName, TransportOverrides = overrides };
        return this;
    }
}
