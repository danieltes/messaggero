# Public API Contract: Multi-Transport Routing

**Feature**: 002-multi-transport-routing  
**Date**: 2026-03-04

## Unchanged Interfaces

The following public interfaces remain **unchanged** to preserve backward compatibility:

- `IMessageBus` — All methods retain their existing signatures.
- `IMessageBusTransport` — No changes.
- `IMessageSerializer` — No changes.
- `IMessageHandler<T>` — No changes.
- `ISubscriptionHandle` — No changes.
- `ITransportSubscription` — No changes.
- `MessagePublishOptions` — No changes (no per-call transport override).
- `SubscriptionOptions` — No changes.
- `MessageEnvelope<T>` — No changes.
- `MessageMetadata` — No changes.
- `ErrorStrategy` — No changes.
- `ReconnectionOptions` — No changes.
- `LifecycleEvent` — No changes (already has `TransportName` field).

## New Types

### ITransportRouter (internal)

```csharp
namespace Messaggero.Routing;

/// <summary>
/// Resolves the target transport for a given destination and optional message type.
/// Internal interface — not exposed to library consumers.
/// </summary>
internal interface ITransportRouter
{
    IMessageBusTransport ResolveTransport(string destination, Type? messageType = null);
    IReadOnlyDictionary<string, IMessageBusTransport> Transports { get; }
}
```

### RoutingRule (internal)

```csharp
namespace Messaggero.Routing;

/// <summary>
/// A single routing rule mapping a destination pattern or message type to a transport.
/// </summary>
internal sealed class RoutingRule
{
    public string TransportName { get; }
    public DestinationPattern? DestinationPattern { get; }
    public Type? MessageType { get; }
}
```

### DestinationPattern (internal)

```csharp
namespace Messaggero.Routing;

/// <summary>
/// A compiled glob pattern for allocation-free destination matching.
/// </summary>
internal sealed class DestinationPattern
{
    public string RawPattern { get; }
    public PatternSpecificity Specificity { get; }
    public bool IsMatch(string destination);
}

internal enum PatternSpecificity
{
    Exact = 0,
    SingleWildcard = 1,
    MultiWildcard = 2
}
```

### TransportHealthEntry (public)

```csharp
namespace Messaggero.Abstractions;

/// <summary>
/// Health status for a single registered transport.
/// </summary>
public sealed class TransportHealthEntry
{
    public string TransportName { get; init; }
    public HealthStatus Status { get; init; }
    public string? Description { get; init; }
}
```

## Modified Types

### HealthCheckResult (extended)

```csharp
namespace Messaggero.Abstractions;

public sealed class HealthCheckResult
{
    // Existing properties (unchanged)
    public HealthStatus Status { get; init; }
    public string? Description { get; init; }

    // New property
    public IReadOnlyList<TransportHealthEntry> TransportEntries { get; init; } = [];
}
```

**Backward compatibility**: When a single transport is registered, `TransportEntries` contains one entry. Existing code that reads only `Status` and `Description` continues to work.

### MessageBusBuilder (extended)

```csharp
namespace Messaggero;

public sealed class MessageBusBuilder
{
    // Existing methods (unchanged)
    public MessageBusBuilder UseTransport(IMessageBusTransport transport);
    public MessageBusBuilder UseSerializer(IMessageSerializer serializer);
    public MessageBusBuilder WithReconnection(Action<ReconnectionOptions> configure);
    public MessageBusBuilder ConfigureServices(Action<IServiceCollection> configure);

    // New methods
    /// <summary>
    /// Registers a named transport. The transport's Name property is used as the key.
    /// </summary>
    public MessageBusBuilder AddTransport(IMessageBusTransport transport);

    /// <summary>
    /// Registers a named transport with an explicit name override.
    /// </summary>
    public MessageBusBuilder AddTransport(string name, IMessageBusTransport transport);

    /// <summary>
    /// Sets the default transport used when no routing rule matches.
    /// </summary>
    public MessageBusBuilder UseDefaultTransport(string transportName);

    /// <summary>
    /// Adds a destination-based routing rule.
    /// </summary>
    public MessageBusBuilder RouteDestination(string destinationPattern, string transportName);

    /// <summary>
    /// Adds a type-based routing rule.
    /// </summary>
    public MessageBusBuilder RouteType<T>(string transportName);

    /// <summary>
    /// Adds a type-based routing rule.
    /// </summary>
    public MessageBusBuilder RouteType(Type messageType, string transportName);
}
```

**Backward compatibility**: `UseTransport(transport)` still works. When called, it registers the transport using its `Name` and marks it as default. Single-transport apps need zero changes.

### Transport Extension Methods (modified)

```csharp
// Messaggero.Transport.Kafka
namespace Messaggero.Transport.Kafka;

public static class ServiceCollectionExtensions
{
    // Existing (backward compatible)
    public static MessageBusBuilder UseKafka(
        this MessageBusBuilder builder, 
        Action<KafkaConfiguration> configure);

    // New overload for multi-transport
    public static MessageBusBuilder AddKafka(
        this MessageBusBuilder builder,
        Action<KafkaConfiguration> configure);

    public static MessageBusBuilder AddKafka(
        this MessageBusBuilder builder,
        string name,
        Action<KafkaConfiguration> configure);
}

// Messaggero.Transport.RabbitMQ
namespace Messaggero.Transport.RabbitMQ;

public static class ServiceCollectionExtensions
{
    // Existing (backward compatible)
    public static MessageBusBuilder UseRabbitMq(
        this MessageBusBuilder builder,
        Action<RabbitMqConfiguration> configure);

    // New overload for multi-transport
    public static MessageBusBuilder AddRabbitMq(
        this MessageBusBuilder builder,
        Action<RabbitMqConfiguration> configure);

    public static MessageBusBuilder AddRabbitMq(
        this MessageBusBuilder builder,
        string name,
        Action<RabbitMqConfiguration> configure);
}
```

**Convention**:
- `Use*` methods = single-transport setup (sets as default, backward compatible)
- `Add*` methods = multi-transport setup (registers a named transport without setting default)

## Usage Examples

### Single Transport (backward compatible, zero changes)

```csharp
services.AddMessaggero(bus => bus
    .UseRabbitMq(config => config.HostName = "localhost"));
```

### Multi-Transport with Destination Routing

```csharp
services.AddMessaggero(bus => bus
    .AddRabbitMq(config => config.HostName = "localhost")
    .AddKafka(config => config.BootstrapServers = "localhost:9092")
    .RouteDestination("orders.*", "Kafka")
    .RouteDestination("notifications.*", "RabbitMQ")
    .UseDefaultTransport("RabbitMQ"));
```

### Multi-Transport with Type Routing

```csharp
services.AddMessaggero(bus => bus
    .AddRabbitMq(config => config.HostName = "localhost")
    .AddKafka(config => config.BootstrapServers = "localhost:9092")
    .RouteType<OrderEvent>("Kafka")
    .RouteType<SendEmailCommand>("RabbitMQ")
    .UseDefaultTransport("RabbitMQ"));
```

### Mixed Routing (Destination + Type)

```csharp
services.AddMessaggero(bus => bus
    .AddRabbitMq(config => config.HostName = "localhost")
    .AddKafka(config => config.BootstrapServers = "localhost:9092")
    .RouteDestination("orders.*", "Kafka")        // destination routes take priority
    .RouteType<AuditEvent>("Kafka")                // type route as fallback
    .UseDefaultTransport("RabbitMQ"));             // everything else → RabbitMQ
```

## Error Scenarios

| Scenario | Error Type | Message |
|----------|-----------|---------|
| RouteDestination references unregistered transport | `InvalidOperationException` | "Routing rule references transport '{name}' which is not registered. Registered transports: {list}." |
| Two destination rules with same pattern, different transports | `InvalidOperationException` | "Conflicting routing rules: destination pattern '{pattern}' is mapped to both '{transport1}' and '{transport2}'." |
| Publish to destination with no matching rule and no default | `InvalidOperationException` | "Cannot route message to destination '{destination}': no matching routing rule and no default transport configured." |
| Publish to destination routed to unavailable transport | `InvalidOperationException` | "Transport '{name}' is not available. The transport failed to connect." |
| Duplicate transport name registration | `InvalidOperationException` | "A transport with name '{name}' is already registered." |
