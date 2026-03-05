# Research: Message Bus Abstraction Library

**Feature**: `001-message-bus-abstraction`  
**Date**: 2026-03-04

## R1. Confluent.Kafka .NET — Producer & Consumer Patterns

### Decision: Single producer, single-threaded consumer, manual offset commit

**Rationale**: `IProducer<TKey, TValue>` is thread-safe → one instance shared across all publish callers. `IConsumer<TKey, TValue>` is NOT thread-safe → one instance per subscription loop thread, dispatch to `KeyPartitionedProcessor` for concurrent processing. Manual offset commit (`EnableAutoCommit = false`, `EnableAutoOffsetStore = false`) is required for at-least-once delivery.

**Alternatives considered**:
- Multiple producer instances: rejected — wastes broker connections; single instance handles 10k+ msg/s with batching.
- Auto-commit offsets: rejected — commits before handler completes, violating at-least-once guarantee.
- Per-message `Commit()`: rejected — too slow; batch commits every N messages or on a timer are preferred.

### Key Configuration

| Property | Value | Notes |
|---|---|---|
| `acks` | `all` | Durability; required for `enable.idempotence` |
| `enable.idempotence` | `true` | Prevents duplicates on retry |
| `compression.type` | `lz4` | Low-latency compression |
| `batch.size` | `131072` (128 KB) | Larger batches for throughput |
| `linger.ms` | `10` | Accumulate batches |
| `EnableAutoCommit` | `false` | At-least-once requires manual commit |
| `EnableAutoOffsetStore` | `false` | Explicit `StoreOffset` after processing |
| `AutoOffsetReset` | `Earliest` | Start from beginning if no committed offset |
| `reconnect.backoff.ms` | `100` | Initial reconnect delay |
| `reconnect.backoff.max.ms` | `10000` | Max reconnect delay (exponential) |

### Reconnection

Confluent.Kafka (librdkafka) handles reconnection internally with built-in exponential backoff. No custom reconnection logic needed for the Kafka transport. The library's `ExponentialBackoffReconnector` is used only by the RabbitMQ transport. Kafka lifecycle events are mapped from `SetErrorHandler()` and `SetStatisticsHandler()`.

### Serialization Integration

`ProducerBuilder.SetValueSerializer(ISerializer<T>)` and `ConsumerBuilder.SetValueDeserializer(IDeserializer<T>)` accept custom implementations. Messaggero's `IMessageSerializer` will be adapted to Confluent's `ISerializer<T>` / `IDeserializer<T>` via thin wrapper classes inside the Kafka transport project.

---

## R2. RabbitMQ.Client 7.x — Connection, Channel & Consumer Patterns

### Decision: Two connections (publish/consume), channel pooling, built-in auto-recovery

**Rationale**: Separate connections isolate publisher TCP I/O from consumer I/O, preventing slow consumers from back-pressuring publishes. Channel pooling (4–8 channels) on the publish connection enables concurrent `BasicPublishAsync` calls with publisher confirms. Built-in `AutomaticRecoveryEnabled` + `TopologyRecoveryEnabled` handles reconnection and re-declaration of exchanges/queues/consumers automatically. One channel per subscription for consuming.

**Alternatives considered**:
- Single connection for both publish and consume: rejected — I/O contention under load.
- Custom reconnection logic (disable auto-recovery): rejected — topology recovery is complex to reimplement; built-in recovery is battle-tested. Lifecycle events are layered on top of `RecoverySucceededAsync` / `ConnectionShutdownAsync`.
- Channel-per-publish call: rejected — channel creation is not free; pooling amortizes cost.

### Key Configuration

| Setting | Value | Notes |
|---|---|---|
| `AutomaticRecoveryEnabled` | `true` | Built-in reconnection |
| `TopologyRecoveryEnabled` | `true` | Re-declares queues/exchanges/consumers on reconnect |
| `NetworkRecoveryInterval` | `5s` | Fixed retry interval (built-in; exponential backoff layered on top for Messaggero lifecycle events) |
| `ConsumerDispatchConcurrency` | `1` | Single dispatch; `KeyPartitionedProcessor` handles concurrency |
| `RequestedHeartbeat` | `60s` | Dead-connection detection |

### Publisher Confirms (at-least-once)

In RabbitMQ.Client 7.x, `BasicPublishAsync` returns a `ValueTask` that completes on broker ack/nack. Call `ConfirmSelectAsync()` once per channel after creation. Mandatory flag + `BasicReturnAsync` event detects unroutable messages.

### Destination Mapping

| Messaggero Concept | RabbitMQ Mapping |
|---|---|
| Destination | Exchange (type: `fanout`, durable) |
| Routing/Partition Key | Routing key on `BasicPublishAsync` |
| Consumer Group | Queue name (`{destination}.{groupId}`, durable) |
| Independent groups | Separate queues bound to same exchange |

### Per-Key Ordering

RabbitMQ does not guarantee per-key ordering with concurrent consumers. Solution: `ConsumerDispatchConcurrency = 1` on the channel, dispatch to `KeyPartitionedProcessor` which maintains per-key sequential processing with cross-key parallelism.

---

## R3. Transport Abstraction Patterns in .NET

### Decision: Separate Abstractions package, builder-pattern DI, `IBus`-style facade

**Rationale**: Following the MassTransit pattern — `Messaggero.Abstractions` contains only interfaces and DTOs (zero dependencies), `Messaggero` contains the orchestrator and DI extensions, transport packages contain broker-specific implementations. This allows consumers to depend on abstractions in domain layers without pulling in broker client libraries.

**Alternatives considered**:
- Monolithic package (all transports in one NuGet): rejected — forces consumers to install all broker client dependencies even if they use only one.
- Abstractions bundled into core package (NServiceBus/Rebus pattern): rejected — prevents clean dependency inversion in layered architectures.

### Interface Hierarchy

```
IMessageBusTransport          — Transport lifecycle (ConnectAsync, DisconnectAsync, HealthCheck)
  └── creates IPublisher, ISubscriber

IPublisher                    — PublishAsync(destination, payload, metadata)
ISubscriber                   — SubscribeAsync(destination, groupId, handler)
IMessageHandler<T>            — HandleAsync(MessageEnvelope<T>, CancellationToken)
IMessageSerializer            — Serialize<T>/Deserialize<T> + ContentType property
```

### DI Registration

```
services.AddMessaggero(bus => bus
    .UseRabbitMq(rabbit => { rabbit.HostName = "..."; })  // or .UseKafka(...)
    .UseJsonSerializer()
    .WithConcurrency(maxConcurrency: 4)
);
```

### Key Differentiator: KeyPartitionedProcessor

No major .NET messaging library builds application-level per-key partitioned dispatching. MassTransit/NServiceBus rely on broker-level ordering (Kafka partitions) or single-consumer-per-queue (RabbitMQ). Messaggero's `KeyPartitionedProcessor` — which enforces per-key sequential processing while allowing cross-key parallelism — is a differentiating feature that works identically across both transports.

---

## R4. Testing Infrastructure — Testcontainers + BenchmarkDotNet

### Decision: Collection fixtures for broker containers, JSON-export baseline comparison for benchmarks

**Rationale**: xUnit collection fixtures share a single broker container across all test classes in a collection, avoiding per-class container spin-up. BenchmarkDotNet JSON export + baseline comparison script provides CI-grade regression detection.

**Alternatives considered**:
- Per-test-class container lifecycle: rejected — too slow; container startup is 5–15 seconds.
- `github-action-benchmark` for regression gate: viable alternative but couples to GitHub Actions; JSON baseline comparison is CI-agnostic.
- `--job dry` for CI benchmarks: rejected — too few iterations for meaningful comparison; `--job short` provides reasonable statistical confidence.

### Testcontainers Setup

- `Testcontainers.RabbitMq` → `RabbitMqBuilder().WithImage("rabbitmq:3-management").Build()`
- `Testcontainers.Kafka` → `KafkaBuilder().WithImage("confluentinc/cp-kafka:7.x").Build()`
- Random port binding to avoid CI conflicts
- Collection fixtures: `RabbitMqFixture`, `KafkaFixture` shared across integration test classes

### BenchmarkDotNet CI

- `--job short --exporters json --memory` for PR-level runs
- `--job medium` post-merge for baseline updates
- Regression thresholds: throughput regression > 10% → fail, p95 latency regression > 15% → fail, allocation increase > 20% → warn
- Compare on `Statistics.Median` for robustness against CI noise

---

## R5. Serialization Strategy

### Decision: `System.Text.Json` with source generators as default, pluggable via `IMessageSerializer`

**Rationale**: `System.Text.Json` is the standard .NET serializer, has excellent performance, and supports AOT via source generators in .NET 10. The `IMessageSerializer` interface includes a `ContentType` property to support multi-format scenarios (e.g., JSON for most messages, Protobuf for high-throughput paths). Kafka transport wraps `IMessageSerializer` in thin `ISerializer<T>` / `IDeserializer<T>` adapters. RabbitMQ transport serializes to `ReadOnlyMemory<byte>` via `ArrayPool<byte>` for zero-copy publishing.

**Alternatives considered**:
- Protobuf/MessagePack as default: rejected — higher barrier to entry; JSON is universally understood and sufficient for initial release.
- No `ContentType` on serializer: rejected — prevents multi-serializer routing in future.

---

## R6. Per-Key Ordering: KeyPartitionedProcessor Design

### Decision: ConcurrentDictionary of per-key Channel\<T\> queues + bounded worker tasks

**Rationale**: When a message arrives, its routing key is hashed to determine a logical partition. Each partition has a `System.Threading.Channels.Channel<MessageEnvelope>` queue and a dedicated consumer task processing sequentially. The number of partitions equals the configured concurrency limit. Messages with the same key always map to the same partition (consistent hashing), guaranteeing per-key ordering while allowing cross-key parallelism.

**Alternatives considered**:
- `ConcurrentDictionary<string, SemaphoreSlim>` per unique key: rejected — unbounded dictionary growth for high-cardinality keys; partition-based approach caps memory at concurrency limit.
- RabbitMQ consistent-hash exchange: rejected — couples ordering solution to a specific transport and requires a broker plugin.
- Single-threaded processing: rejected — caps throughput at one message at a time regardless of key diversity.

### Algorithm

1. Incoming message with key `K` → partition index = `MurmurHash(K) % concurrencyLimit`
2. Enqueue to `partitions[index].Writer.WriteAsync(envelope)`
3. Each partition has a long-running consumer task: `while (await reader.ReadAsync())` → invoke handler → ack
4. If key is null → round-robin across partitions (no ordering guarantee for null keys, matching Kafka's behavior)
