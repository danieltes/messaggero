# Public API Contracts: Message Bus Abstraction Library

**Feature**: `001-message-bus-abstraction`  
**Date**: 2026-03-04  
**Package**: `Messaggero.Abstractions`

This document defines the public interfaces that transport implementations must satisfy
and that consumers program against. These contracts are the library's primary stability
surface — breaking changes require a MAJOR version bump per the constitution.

---

## IMessageBus

Top-level facade combining publish, subscribe, and lifecycle management.
Registered as a singleton in DI. Consumers interact with this interface only.

```csharp
public interface IMessageBus : IAsyncDisposable
{
    /// Publish a message to the specified destination.
    Task PublishAsync<T>(
        string destination,
        T payload,
        MessagePublishOptions? options = null,
        CancellationToken cancellationToken = default);

    /// Subscribe to messages on the specified destination.
    /// Returns a handle that can be used to unsubscribe.
    Task<ISubscriptionHandle> SubscribeAsync<T>(
        string destination,
        string groupId,
        IMessageHandler<T> handler,
        SubscriptionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// Query the health of the active transport connection.
    Task<HealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken = default);

    /// Register a listener for lifecycle events.
    IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener);
}
```

### MessagePublishOptions

```csharp
public sealed record MessagePublishOptions
{
    public string? RoutingKey { get; init; }
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
    public string? CorrelationId { get; init; }
}
```

### ISubscriptionHandle

```csharp
public interface ISubscriptionHandle : IAsyncDisposable
{
    string Destination { get; }
    string GroupId { get; }
    bool IsActive { get; }
}
```

---

## IMessageHandler\<T\>

Implemented by consumers to process incoming messages.

```csharp
public interface IMessageHandler<in T>
{
    Task HandleAsync(
        MessageEnvelope<T> envelope,
        CancellationToken cancellationToken = default);
}
```

---

## IMessageBusTransport

Implemented by transport packages (e.g., `Messaggero.Transport.RabbitMQ`).
NOT consumed directly by application code — used by the `Messaggero` core orchestrator.

```csharp
public interface IMessageBusTransport : IAsyncDisposable
{
    string Name { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    Task PublishAsync(
        string destination,
        ReadOnlyMemory<byte> body,
        MessageMetadata metadata,
        CancellationToken cancellationToken = default);

    Task<ITransportSubscription> SubscribeAsync(
        string destination,
        string groupId,
        Func<ReadOnlyMemory<byte>, MessageMetadata, CancellationToken, Task> handler,
        SubscriptionOptions options,
        CancellationToken cancellationToken = default);

    Task<HealthCheckResult> CheckHealthAsync(
        CancellationToken cancellationToken = default);

    IDisposable OnLifecycleEvent(Action<LifecycleEvent> listener);
}
```

### MessageMetadata

Wire-level metadata passed between core and transport (not exposed to consumers directly).

```csharp
public sealed record MessageMetadata
{
    public required string MessageId { get; init; }
    public string? RoutingKey { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string ContentType { get; init; } = "application/json";
    public string? CorrelationId { get; init; }
}
```

### ITransportSubscription

```csharp
public interface ITransportSubscription : IAsyncDisposable
{
    bool IsActive { get; }
}
```

---

## IMessageSerializer

Pluggable serialization contract.

```csharp
public interface IMessageSerializer
{
    string ContentType { get; }
    byte[] Serialize<T>(T value);
    T Deserialize<T>(ReadOnlySpan<byte> data);
}
```

**Default implementation**: `JsonMessageSerializer` using `System.Text.Json`.

---

## MessageEnvelope\<T\>

Immutable record delivered to message handlers.

```csharp
public sealed record MessageEnvelope<T>
{
    public required string MessageId { get; init; }
    public required T Payload { get; init; }
    public required string Destination { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }
        = new Dictionary<string, string>();
    public string? RoutingKey { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string ContentType { get; init; } = "application/json";
    public string? CorrelationId { get; init; }
}
```

---

## SubscriptionOptions

```csharp
public sealed record SubscriptionOptions
{
    public int MaxConcurrency { get; init; } = 1;
    public ErrorStrategy ErrorStrategy { get; init; } = ErrorStrategy.Retry();
    public int? PrefetchCount { get; init; }
}
```

---

## ErrorStrategy

```csharp
public sealed record ErrorStrategy
{
    public ErrorStrategyType Type { get; init; }
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double RetryBackoffMultiplier { get; init; } = 2.0;
    public string? DeadLetterDestination { get; init; }

    public static ErrorStrategy Retry(
        int maxRetries = 3,
        TimeSpan? delay = null,
        double multiplier = 2.0)
        => new()
        {
            Type = ErrorStrategyType.Retry,
            MaxRetries = maxRetries,
            RetryDelay = delay ?? TimeSpan.FromSeconds(1),
            RetryBackoffMultiplier = multiplier
        };

    public static ErrorStrategy DeadLetter(string destination)
        => new() { Type = ErrorStrategyType.DeadLetter, DeadLetterDestination = destination };

    public static ErrorStrategy Reject()
        => new() { Type = ErrorStrategyType.Reject };
}

public enum ErrorStrategyType
{
    Retry,
    DeadLetter,
    Reject
}
```

---

## LifecycleEvent

```csharp
public sealed record LifecycleEvent
{
    public required LifecycleEventType EventType { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string TransportName { get; init; }
    public string? Destination { get; init; }
    public string? MessageId { get; init; }
    public Exception? Error { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

public enum LifecycleEventType
{
    TransportConnected,
    TransportDisconnected,
    TransportReconnecting,
    TransportFailed,
    MessagePublished,
    MessageReceived,
    MessageError
}
```

---

## HealthCheckResult

```csharp
public sealed record HealthCheckResult
{
    public required bool IsHealthy { get; init; }
    public required string TransportName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}
```

---

## DI Registration (Messaggero core package)

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessaggero(
        this IServiceCollection services,
        Action<MessageBusBuilder> configure);
}

public sealed class MessageBusBuilder
{
    public MessageBusBuilder UseTransport<TTransport>(
        Action<object> configure) where TTransport : IMessageBusTransport;

    public MessageBusBuilder UseSerializer(IMessageSerializer serializer);

    public MessageBusBuilder WithReconnection(Action<ReconnectionOptions> configure);
}
```

## DI Registration (Transport packages)

```csharp
// Messaggero.Transport.RabbitMQ
public static class RabbitMqServiceCollectionExtensions
{
    public static MessageBusBuilder UseRabbitMq(
        this MessageBusBuilder builder,
        Action<RabbitMqConfiguration> configure);
}

// Messaggero.Transport.Kafka
public static class KafkaServiceCollectionExtensions
{
    public static MessageBusBuilder UseKafka(
        this MessageBusBuilder builder,
        Action<KafkaConfiguration> configure);
}
```
