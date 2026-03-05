using Messaggero.Abstractions;
using Messaggero.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Messaggero;

/// <summary>
/// Builder for configuring the message bus transport, serializer, and reconnection options.
/// </summary>
public sealed class MessageBusBuilder
{
    internal IMessageSerializer? Serializer { get; private set; }
    internal ReconnectionOptions? Reconnection { get; private set; }
    internal List<Action<IServiceCollection>> ServiceActions { get; } = [];
    internal Dictionary<string, IMessageBusTransport> TransportRegistry { get; } = new(StringComparer.Ordinal);
    internal List<RoutingRule> DestinationRules { get; } = [];
    internal List<RoutingRule> TypeRules { get; } = [];
    internal string? DefaultTransportName { get; private set; }

    /// <summary>
    /// Configures a transport implementation for the message bus.
    /// Backward compatible: registers the transport by its Name and sets it as the default.
    /// </summary>
    /// <param name="transport">The transport implementation to use.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder UseTransport(IMessageBusTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        AddTransportInternal(transport.Name, transport);
        DefaultTransportName = transport.Name;
        return this;
    }

    /// <summary>
    /// Registers a named transport. The transport's Name property is used as the key.
    /// </summary>
    /// <param name="transport">The transport to register.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder AddTransport(IMessageBusTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        AddTransportInternal(transport.Name, transport);
        return this;
    }

    /// <summary>
    /// Registers a named transport with an explicit name override.
    /// </summary>
    /// <param name="name">The name to register the transport under.</param>
    /// <param name="transport">The transport to register.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder AddTransport(string name, IMessageBusTransport transport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(transport);
        AddTransportInternal(name, transport);
        return this;
    }

    /// <summary>
    /// Sets the default transport used when no routing rule matches.
    /// </summary>
    /// <param name="transportName">Name of the default transport.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder UseDefaultTransport(string transportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        DefaultTransportName = transportName;
        return this;
    }

    /// <summary>
    /// Adds a destination-based routing rule.
    /// </summary>
    /// <param name="destinationPattern">The destination glob pattern.</param>
    /// <param name="transportName">The target transport name.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder RouteDestination(string destinationPattern, string transportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        DestinationRules.Add(RoutingRule.ForDestination(transportName, destinationPattern));
        return this;
    }

    /// <summary>
    /// Adds a type-based routing rule.
    /// </summary>
    /// <typeparam name="T">The message type to route.</typeparam>
    /// <param name="transportName">The target transport name.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder RouteType<T>(string transportName)
    {
        return RouteType(typeof(T), transportName);
    }

    /// <summary>
    /// Adds a type-based routing rule.
    /// </summary>
    /// <param name="messageType">The message type to route.</param>
    /// <param name="transportName">The target transport name.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder RouteType(Type messageType, string transportName)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(transportName);
        TypeRules.Add(RoutingRule.ForType(transportName, messageType));
        return this;
    }

    /// <summary>
    /// Sets a custom message serializer. If not called, the default JSON serializer is used.
    /// </summary>
    /// <param name="serializer">The serializer implementation.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder UseSerializer(IMessageSerializer serializer)
    {
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        return this;
    }

    /// <summary>
    /// Configures reconnection behavior with exponential backoff.
    /// </summary>
    /// <param name="configure">An action to configure reconnection options.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder WithReconnection(Action<ReconnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new ReconnectionOptions();
        configure(options);
        options.Validate();
        Reconnection = options;
        return this;
    }

    /// <summary>
    /// Registers additional services required by transport extensions.
    /// </summary>
    /// <param name="configure">An action to configure additional services.</param>
    /// <returns>This builder for chaining.</returns>
    public MessageBusBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ServiceActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Validates the builder configuration and throws descriptive errors if required components are missing.
    /// Called internally during DI registration to enable fail-fast behavior.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when transport is not configured.</exception>
    internal void Validate()
    {
        if (TransportRegistry.Count == 0)
        {
            throw new InvalidOperationException(
                "No transport configured. Call UseRabbitMq() or UseKafka() on the MessageBusBuilder. " +
                "Example: builder.AddMessaggero(bus => bus.UseRabbitMq(config => config.HostName = \"localhost\"));");
        }

        // Validate routing rules by constructing a TransportRouter (which performs all checks)
        _ = BuildRouter();
    }

    /// <summary>
    /// Builds the transport router from the current configuration.
    /// </summary>
    internal TransportRouter BuildRouter()
    {
        return new TransportRouter(
            TransportRegistry,
            DestinationRules,
            TypeRules,
            DefaultTransportName);
    }

    private void AddTransportInternal(string name, IMessageBusTransport transport)
    {
        if (!TransportRegistry.TryAdd(name, transport))
        {
            throw new InvalidOperationException(
                $"A transport with name '{name}' is already registered.");
        }
    }
}
