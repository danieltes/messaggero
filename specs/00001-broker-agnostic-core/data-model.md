# Data Model: Broker-Agnostic Messaging Library

**Feature**: 00001-broker-agnostic-core  
**Date**: 2026-03-29

## Entities

### Message

The envelope transmitted through the library for both publishing and consuming.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | `string` | Yes | Unique identifier. Library-generated (GUID) before routing; caller MAY override. |
| `Type` | `string` | Yes | Message type identifier. Primary key for routing decisions. |
| `Payload` | `ReadOnlyMemory<byte>` | Yes | Serialized message body (opaque bytes). |
| `Headers` | `MessageHeaders` | Yes | Key-value metadata (string→string). Includes `content-type`, custom headers. |
| `Timestamp` | `DateTimeOffset` | Yes | When the message was created. Library-set at publish time; caller MAY override. |
| `SourceTransport` | `string?` | No | Populated on consumed messages only. Name of the transport that delivered the message. |

**Validation rules**:
- `Id`: Non-empty. If caller-provided, must be non-whitespace.
- `Type`: Non-empty, non-whitespace. Must match a registered routing rule at publish time (or `NoRouteFoundException`).
- `Payload`: May be empty (`ReadOnlyMemory<byte>.Empty`) but not null conceptually.

---

### MessageHeaders

Wrapper around header key-value pairs with typed accessors.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ContentType` | `string` | Yes | MIME type of payload (e.g., `application/json`). Set by serializer. |
| `CorrelationId` | `string?` | No | Optional correlation identifier for tracing message chains. |
| `Custom` | `IReadOnlyDictionary<string, string>` | No | Arbitrary user-defined headers. |

---

### Destination

Broker-agnostic reference to a publish/subscribe target.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | `string` | Yes | Logical destination name (e.g., `"orders"`, `"emails"`). |
| `TransportOverrides` | `IReadOnlyDictionary<string, string>?` | No | Adapter-specific overrides (e.g., Kafka partition key, RabbitMQ routing key). |

**Broker mapping**:
- Kafka: `Name` → topic name.
- RabbitMQ: `Name` → exchange name; routing key from `TransportOverrides["routingKey"]` or defaults to `Name`.

---

### RoutingRule

Maps a message type to one or more named transport registrations.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `MessageType` | `string` | Yes | The message type this rule applies to. |
| `Transports` | `IReadOnlyList<string>` | Yes | Named transport registrations to deliver to. Single = direct, multiple = fan-out. |
| `Destination` | `Destination?` | No | Override destination; if null, uses default destination for the message type. |

**Validation rules**:
- `MessageType`: Non-empty; must be unique across routing rules (one rule per type).
- `Transports`: Non-empty list; each name must match a registered adapter.
- Validated at build time. `TransportNotFoundException` if a named transport is not registered.

---

### RoutingTable

Keyed collection of `RoutingRule`s consulted at publish time.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Rules` | `IReadOnlyDictionary<string, RoutingRule>` | Yes | Keyed by `MessageType` for O(1) lookup. |

**State transitions**: Immutable after `MessagingBuilder.Build()`. No runtime modifications.

**Performance**: Dictionary lookup → O(1) average. Meets NFR-002 (≤0.5ms p99 for ≤1,000 rules).

---

### RetryPolicy

Per-transport configuration for message processing retry behavior.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MaxAttempts` | `int` | No | `3` | Maximum retry attempts (including first attempt). |
| `BackoffStrategy` | `BackoffStrategy` | No | `Exponential` | `Fixed` or `Exponential`. |
| `InitialDelay` | `TimeSpan` | No | `1s` | Delay before first retry. |
| `MaxDelay` | `TimeSpan` | No | `30s` | Cap on backoff delay (for exponential). |
| `RetryableExceptions` | `Func<Exception, bool>?` | No | All exceptions | Predicate to filter which exceptions trigger retry. |
| `DeadLetterDestination` | `Destination?` | No | null | Where to route messages after retries exhausted. |

**Validation rules**:
- `MaxAttempts` ≥ 1.
- `InitialDelay` > 0.
- `MaxDelay` ≥ `InitialDelay`.
- If `DeadLetterDestination` is set, the destination must be reachable from the owning transport.

---

### PublishResult

Typed outcome of a publish operation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `MessageId` | `string` | Yes | The published message's ID. |
| `Outcomes` | `IReadOnlyList<TransportOutcome>` | Yes | Per-transport results (one per target in fan-out). |
| `IsSuccess` | `bool` | Yes (computed) | True if ALL transport outcomes succeeded. |

---

### TransportOutcome

Per-transport result within a `PublishResult`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `TransportName` | `string` | Yes | Named transport that handled this outcome. |
| `Success` | `bool` | Yes | Whether delivery to this transport succeeded. |
| `BrokerMetadata` | `IReadOnlyDictionary<string, string>?` | No | Adapter-specific ack metadata (e.g., Kafka offset/partition). |
| `Error` | `PublishFailure?` | No | Populated on failure. |

---

### HandlerRegistration

Internal model binding a handler type to message type and configuration.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `MessageType` | `string` | Yes | — | Message type this handler processes. |
| `HandlerType` | `Type` | Yes | — | CLR type implementing `IMessageHandler<T>`. |
| `TransportScope` | `string?` | No | `null` (all transports) | If set, handler receives messages from this transport only. |
| `MaxConcurrency` | `int` | No | `1` | Max concurrent handler invocations. |

---

### TransportRegistration

Internal model for a registered transport adapter.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | `string` | Yes | Unique name identifying this transport (e.g., `"kafka"`, `"rabbitmq"`). |
| `AdapterFactory` | `Func<IServiceProvider, ITransportAdapter>` | Yes | Factory for creating the adapter instance. |
| `Options` | `object` | Yes | Transport-specific options (e.g., `KafkaOptions`, `RabbitMqOptions`). |
| `RetryPolicy` | `RetryPolicy` | No | Per-transport retry configuration. |
| `Serializer` | `IMessageSerializer?` | No | Override serializer for this transport. Null = default JSON. |

---

### MessagingConfiguration

Immutable snapshot produced by `MessagingBuilder.Build()`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Transports` | `IReadOnlyDictionary<string, TransportRegistration>` | Yes | All registered transports. |
| `RoutingTable` | `RoutingTable` | Yes | Routing rules. |
| `Handlers` | `IReadOnlyList<HandlerRegistration>` | Yes | All handler registrations. |
| `DefaultSerializer` | `IMessageSerializer` | Yes | Default serializer (JSON). |
| `ObservabilityEnabled` | `bool` | No | Whether structured logging/metrics/tracing is active. |

**State transitions**: Created once by builder. Immutable at runtime. Consumed by `MessagingHost`.

---

## Error Types

| Error Type | Base | Context |
|------------|------|---------|
| `PublishFailure` | `MessagingException` | Broker rejected or was unreachable during publish. Contains transport name and broker error. Used both as a thrown exception and as `TransportOutcome.Error` data. |
| `NoRouteFoundException` | `MessagingException` | No routing rule matches the published message type. |
| `TransportNotFoundException` | `MessagingException` | A routing rule or handler scope references a transport name not registered. Thrown at build time. |
| `TransportDegradedException` | `MessagingException` | A transport's connection was lost at runtime. Emitted as event; does not stop other transports. |
| `DeserializationException` | `MessagingException` | Payload deserialization failed. Message routed to dead-letter. |
| `RetryExhaustedException` | `MessagingException` | Handler processing failed after all retry attempts. Message routed to dead-letter. |
| `MessagingConfigurationException` | `MessagingException` | Builder validation failed (invalid routing, missing transports, etc.). Thrown at build time. |

---

## Enumerations

### BackoffStrategy

| Value | Description |
|-------|-------------|
| `Fixed` | Constant delay between retries. |
| `Exponential` | Delay doubles each attempt, capped at `MaxDelay`. |

---

## Relationships

```
MessagingBuilder ──builds──► MessagingConfiguration
MessagingConfiguration ──contains──► RoutingTable
MessagingConfiguration ──contains──► TransportRegistration[]
MessagingConfiguration ──contains──► HandlerRegistration[]
RoutingTable ──contains──► RoutingRule[]
RoutingRule ──references──► TransportRegistration (by name)
RoutingRule ──optionally-has──► Destination
HandlerRegistration ──optionally-scoped-to──► TransportRegistration (by name)
TransportRegistration ──has──► RetryPolicy
TransportRegistration ──has──► IMessageSerializer
MessagingHost ──consumes──► MessagingConfiguration
MessagingHost ──manages──► ITransportAdapter[] (created from TransportRegistration.AdapterFactory)
Message ──routed-by──► RoutingRule (via Message.Type)
Message ──serialized-by──► IMessageSerializer
PublishResult ──contains──► TransportOutcome[]
```
