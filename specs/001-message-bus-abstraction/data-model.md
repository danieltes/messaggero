# Data Model: Message Bus Abstraction Library

**Feature**: `001-message-bus-abstraction`  
**Date**: 2026-03-04  
**Source**: [spec.md](spec.md) Key Entities + [research.md](research.md) design decisions

## Entities

### MessageEnvelope\<T\>

Transport-agnostic container for a single message.

| Field | Type | Required | Description |
|---|---|---|---|
| MessageId | `string` | Yes | Unique identifier (UUID v7 recommended for time-ordering) |
| Payload | `T` | Yes | Deserialized message body |
| Headers | `IReadOnlyDictionary<string, string>` | Yes | Key-value metadata (may be empty) |
| Destination | `string` | Yes | Logical destination name (topic/queue) |
| RoutingKey | `string?` | No | Routing/partition key; determines ordering scope |
| Timestamp | `DateTimeOffset` | Yes | When the message was created |
| ContentType | `string` | Yes | Serialization format (e.g., `"application/json"`) |
| CorrelationId | `string?` | No | For request-reply and tracing correlation |

**Validation rules**:
- `MessageId` MUST be non-empty.
- `Destination` MUST be non-empty and contain only alphanumeric characters, hyphens, underscores, and dots.
- `Timestamp` MUST be set; defaults to `DateTimeOffset.UtcNow` if not provided.
- `ContentType` MUST match the active serializer's content type.

**State transitions**: None — envelopes are immutable value objects.

---

### TransportConfiguration

Structured object describing which transport to use and broker-specific settings.

| Field | Type | Required | Description |
|---|---|---|---|
| TransportType | `string` | Yes | Transport identifier (e.g., `"RabbitMQ"`, `"Kafka"`) |
| Properties | `IReadOnlyDictionary<string, object>` | Yes | Transport-specific configuration key-value pairs |

**Specialized subtypes** (one per transport):

#### RabbitMqConfiguration

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| HostName | `string` | Yes | — | Broker hostname |
| Port | `int` | No | `5672` | AMQP port |
| UserName | `string` | No | `"guest"` | Auth username |
| Password | `string` | No | `"guest"` | Auth password |
| VirtualHost | `string` | No | `"/"` | RabbitMQ virtual host |
| PublishChannelPoolSize | `int` | No | `4` | Number of pooled publish channels |
| HeartbeatInterval | `TimeSpan` | No | `60s` | TCP heartbeat interval |
| NetworkRecoveryInterval | `TimeSpan` | No | `5s` | Auto-recovery retry interval |
| UseSsl | `bool` | No | `false` | Enable TLS |

#### KafkaConfiguration

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| BootstrapServers | `string` | Yes | — | Comma-separated broker list |
| Acks | `string` | No | `"all"` | Producer acknowledgment level |
| EnableIdempotence | `bool` | No | `true` | Idempotent producer |
| CompressionType | `string` | No | `"lz4"` | Compression codec |
| BatchSize | `int` | No | `131072` | Producer batch size in bytes |
| LingerMs | `int` | No | `10` | Batch accumulation delay |
| SessionTimeoutMs | `int` | No | `45000` | Consumer group session timeout |
| HeartbeatIntervalMs | `int` | No | `3000` | Consumer heartbeat interval |
| SecurityProtocol | `string` | No | `"Plaintext"` | Security protocol |

**Validation rules**:
- `TransportType` MUST match a registered transport.
- Transport-specific required fields (e.g., `HostName`, `BootstrapServers`) MUST fail fast with actionable error on missing.
- Port ranges validated (1–65535).
- Pool sizes validated (≥ 1).

---

### SubscriptionOptions

Configuration for a single subscription.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| Destination | `string` | Yes | — | Logical destination name |
| GroupId | `string` | Yes | — | Consumer group identifier |
| MaxConcurrency | `int` | No | `1` | Max concurrent message processing |
| ErrorStrategy | `ErrorStrategy` | No | `Retry(3, exponential)` | Error-handling policy |
| PrefetchCount | `int` | No | = `MaxConcurrency` | Broker-level prefetch (RabbitMQ) |

**Validation rules**:
- `Destination` and `GroupId` MUST be non-empty.
- `MaxConcurrency` MUST be ≥ 1.
- `PrefetchCount` MUST be ≥ `MaxConcurrency`.

---

### ErrorStrategy

Policy applied when a message handler fails.

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| Type | `ErrorStrategyType` (enum) | Yes | `Retry` | Strategy kind |
| MaxRetries | `int` | Conditional | `3` | Max retry count (when Type = Retry) |
| RetryDelay | `TimeSpan` | Conditional | `1s` | Initial retry delay (when Type = Retry) |
| RetryBackoffMultiplier | `double` | Conditional | `2.0` | Exponential backoff multiplier |
| DeadLetterDestination | `string?` | Conditional | `null` | Target for dead-lettered messages (when Type = DeadLetter) |

**ErrorStrategyType enum**: `Retry`, `DeadLetter`, `Reject`

**State transitions**:
1. Handler fails → if `Retry` and attempts < `MaxRetries` → re-invoke handler after delay
2. Handler fails → if `Retry` and attempts ≥ `MaxRetries` → forward to `DeadLetterDestination` if set, else `Reject`
3. Handler fails → if `DeadLetter` → immediately forward to `DeadLetterDestination`
4. Handler fails → if `Reject` → nack message (no requeue) + log

---

### LifecycleEvent

Structured notification emitted at key moments.

| Field | Type | Required | Description |
|---|---|---|---|
| EventType | `LifecycleEventType` (enum) | Yes | Event kind |
| Timestamp | `DateTimeOffset` | Yes | When the event occurred |
| TransportType | `string` | Yes | Which transport emitted it |
| Destination | `string?` | No | Relevant destination (if applicable) |
| MessageId | `string?` | No | Relevant message ID (if applicable) |
| Error | `Exception?` | No | Error details (if applicable) |
| Metadata | `IReadOnlyDictionary<string, object>?` | No | Additional context |

**LifecycleEventType enum**:
- `TransportConnected`
- `TransportDisconnected`
- `TransportReconnecting`
- `TransportFailed`
- `MessagePublished`
- `MessageReceived`
- `MessageError`

**State transitions**: None — events are immutable records.

---

### ReconnectionOptions

Configuration for exponential backoff reconnection (used by RabbitMQ transport layered on top of built-in recovery; Kafka handles reconnection internally).

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| InitialDelay | `TimeSpan` | No | `1s` | First retry delay |
| Multiplier | `double` | No | `2.0` | Exponential backoff multiplier |
| MaxDelay | `TimeSpan` | No | `30s` | Maximum delay between retries |
| MaxAttempts | `int` | No | `10` | Maximum reconnection attempts (0 = unlimited) |

**Validation rules**:
- `InitialDelay` MUST be > 0.
- `Multiplier` MUST be ≥ 1.0.
- `MaxDelay` MUST be ≥ `InitialDelay`.
- `MaxAttempts` MUST be ≥ 0.

## Relationships

```
TransportConfiguration ──1:1──▶ IMessageBusTransport
                                   ├──creates──▶ IPublisher
                                   └──creates──▶ ISubscriber (per subscription)
                                                    │
SubscriptionOptions ──1:1──────────────────────────┘
     │
     └── ErrorStrategy ──1:1──▶ (embedded)

IPublisher ──publishes──▶ MessageEnvelope<T>
ISubscriber ──delivers──▶ MessageEnvelope<T> ──to──▶ IMessageHandler<T>

IMessageBusTransport ──emits──▶ LifecycleEvent

ReconnectionOptions ──1:1──▶ IMessageBusTransport (optional, transport-specific)
```
