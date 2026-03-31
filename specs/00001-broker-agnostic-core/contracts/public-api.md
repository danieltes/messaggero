# Public API Contract: Messaggero Core

**Package**: `Messaggero`  
**Date**: 2026-03-29

This document defines the public interfaces and extension points of the core library package.

---

## IMessageBus

The primary publish/subscribe surface for application code.

```csharp
namespace Messaggero.Abstractions;

public interface IMessageBus
{
    /// <summary>
    /// Publishes a message. The library resolves the target transport(s)
    /// from the routing table based on the message's type.
    /// </summary>
    Task<PublishResult> PublishAsync<TMessage>(
        TMessage message,
        CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>
    /// Publishes a message with explicit headers.
    /// </summary>
    Task<PublishResult> PublishAsync<TMessage>(
        TMessage message,
        MessageHeaders headers,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
```

**Behavioral contract**:
- Resolves routing rule by `typeof(TMessage).Name`.
- Serializes via the transport's configured `IMessageSerializer`.
- Returns `PublishResult` with per-transport outcomes.
- Throws `NoRouteFoundException` if no routing rule matches.
- Never silently drops messages.

---

## IMessageHandler\<TMessage\>

The handler contract for consuming messages.

```csharp
namespace Messaggero.Abstractions;

public interface IMessageHandler<in TMessage> where TMessage : class
{
    /// <summary>
    /// Processes a single message. Called by the library's dispatch loop.
    /// </summary>
    Task HandleAsync(
        TMessage message,
        MessageContext context,
        CancellationToken cancellationToken);
}
```

**Lifecycle hooks** (optional, implement if needed):

```csharp
namespace Messaggero.Abstractions;

public interface IHandlerLifecycle
{
    /// <summary>Called once when the messaging host starts.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Called once when the messaging host stops.</summary>
    Task DisposeAsync();
}
```

**Behavioral contract**:
- `HandleAsync` is invoked per message delivery.
- If the handler throws, the library retries per the transport's `RetryPolicy`.
- After retries exhausted, message is dead-lettered and `RetryExhaustedException` emitted.
- Handlers implementing `IHandlerLifecycle` have hooks called at host start/stop.
- Handlers are independently unit-testable (no library host required).

---

## ITransportAdapter

The adapter contract that broker-specific packages implement.

```csharp
namespace Messaggero.Abstractions;

public interface ITransportAdapter : IAsyncDisposable
{
    /// <summary>Unique name identifying this adapter instance.</summary>
    string Name { get; }

    /// <summary>Starts the adapter (connections, consumers).</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Gracefully stops the adapter (drain, close).</summary>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>Publishes a serialized message to the broker.</summary>
    Task<TransportOutcome> PublishAsync(
        Message message,
        Destination destination,
        CancellationToken cancellationToken);

    /// <summary>
    /// Subscribes to messages for the given destination.
    /// The callback is invoked for each received message.
    /// </summary>
    Task SubscribeAsync(
        Destination destination,
        Func<Message, CancellationToken, Task> onMessage,
        CancellationToken cancellationToken);

    /// <summary>Acknowledges successful processing of a message.</summary>
    Task AcknowledgeAsync(Message message, CancellationToken cancellationToken);

    /// <summary>Negatively acknowledges a message (trigger redelivery or DLQ).</summary>
    Task RejectAsync(Message message, CancellationToken cancellationToken);
}
```

**Behavioral contract**:
- `StartAsync`/`StopAsync` manage connection lifecycle.
- `PublishAsync` returns `TransportOutcome` with success/failure and broker metadata.
- `SubscribeAsync` registers a callback; adapter pushes messages as they arrive.
- `AcknowledgeAsync` commits the message offset/delivery-tag.
- `RejectAsync` nacks the message (broker handles redelivery or DLX routing).
- Adapter MUST be independently stoppable without affecting other adapters (NFR-006).

---

## IMessageSerializer

Pluggable serialization interface.

```csharp
namespace Messaggero.Abstractions;

public interface IMessageSerializer
{
    /// <summary>Serializes a message to bytes.</summary>
    byte[] Serialize<TMessage>(TMessage message) where TMessage : class;

    /// <summary>Deserializes bytes to a message.</summary>
    TMessage Deserialize<TMessage>(ReadOnlySpan<byte> data) where TMessage : class;

    /// <summary>The content type this serializer produces (e.g., "application/json").</summary>
    string ContentType { get; }
}
```

**Behavioral contract**:
- `Deserialize` MUST ignore unknown fields (tolerant reader, FR-018).
- `Deserialize` MUST throw `DeserializationException` on failure (not transport-level exceptions).
- Default implementation: `JsonMessageSerializer` using `System.Text.Json`.

---

## MessageContext

Metadata available to handlers during message processing.

```csharp
namespace Messaggero.Abstractions;

public sealed class MessageContext
{
    /// <summary>Unique message ID.</summary>
    public required string MessageId { get; init; }

    /// <summary>Message type string.</summary>
    public required string MessageType { get; init; }

    /// <summary>Name of the transport that delivered this message.</summary>
    public required string SourceTransport { get; init; }

    /// <summary>Message headers.</summary>
    public required MessageHeaders Headers { get; init; }

    /// <summary>Message timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Current retry attempt (1 = first delivery, 2+ = retry).</summary>
    public required int DeliveryAttempt { get; init; }
}
```

---

## MessagingBuilder (Fluent API)

Entry point for configuration.

```csharp
namespace Messaggero.Configuration;

public sealed class MessagingBuilder
{
    /// <summary>Registers a named transport adapter.</summary>
    public MessagingBuilder AddTransport(
        string name,
        Func<IServiceProvider, ITransportAdapter> factory,
        Action<TransportOptions>? configure = null);

    /// <summary>Defines a routing rule for a message type.</summary>
    public MessagingBuilder Route<TMessage>(
        Action<RoutingRuleBuilder> configure)
        where TMessage : class;

    /// <summary>Registers a message handler.</summary>
    public MessagingBuilder RegisterHandler<THandler, TMessage>(
        Action<HandlerOptions>? configure = null)
        where THandler : class, IMessageHandler<TMessage>
        where TMessage : class;

    /// <summary>Enables structured observability (logs, metrics, traces).</summary>
    public MessagingBuilder EnableObservability();

    /// <summary>
    /// Validates configuration and builds the immutable configuration.
    /// Throws MessagingConfigurationException on validation failure.
    /// </summary>
    public MessagingConfiguration Build();
}
```

**DI integration** (extension method on `IServiceCollection`):

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaggero(
        this IServiceCollection services,
        Action<MessagingBuilder> configure);
}
```

**Adapter extension methods** (in adapter packages):

```csharp
// In Messaggero.Kafka
namespace Messaggero.Kafka;

public static class KafkaBuilderExtensions
{
    public static MessagingBuilder AddKafka(
        this MessagingBuilder builder,
        string name,
        Action<KafkaOptions> configure);
}

// In Messaggero.RabbitMQ
namespace Messaggero.RabbitMQ;

public static class RabbitMqBuilderExtensions
{
    public static MessagingBuilder AddRabbitMQ(
        this MessagingBuilder builder,
        string name,
        Action<RabbitMqOptions> configure);
}
```
