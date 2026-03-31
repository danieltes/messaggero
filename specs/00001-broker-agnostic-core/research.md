# Research: Broker-Agnostic Messaging Library with Multi-Transport Routing

**Feature**: 00001-broker-agnostic-core  
**Date**: 2026-03-29

## R1: Confluent.Kafka .NET — Adapter Design Best Practices

**Decision**: Use `Confluent.Kafka` (`IProducer<string, byte[]>`, `IConsumer<string, byte[]>`) with `byte[]` value type; handle serialization at the library layer via `ISerializer`.

**Rationale**:
- `IProducer<TKey, TValue>` and `IConsumer<TKey, TValue>` are the primary interfaces. Using `byte[]` as the value type lets Messaggero own serialization/deserialization through its own `ISerializer` interface, decoupling from Confluent's built-in serializer registration.
- `ProduceAsync` returns a `DeliveryResult` with offset/partition metadata — maps cleanly to `PublishResult`.
- `IConsumer.Consume(CancellationToken)` is a blocking poll-based loop. Wrap in a background `Task` per consumer with `Task.Run` + cancellation token for graceful shutdown.
- `EnableAutoCommit = false` + manual `StoreOffset` / `Commit` after handler success implements at-least-once semantics (FR-021).
- `MaxPartitionFetchBytes` and `QueuedMinMessages` / `QueuedMaxMessagesKbytes` control prefetch (FR-019).
- Error handling: catch `ProduceException<TKey, TValue>` → map to `PublishFailure`; catch `ConsumeException` → log + retry per policy.
- Producer is thread-safe; share one producer per Kafka adapter instance. Consumer is NOT thread-safe; one consumer loop per subscription. Handle concurrency >1 via `Channel<T>` or `SemaphoreSlim` dispatching.
- Use `StatisticsHandler` callback for metrics emission (FR-011).

**Alternatives considered**:
- `Confluent.SchemaRegistry` for Avro/Protobuf serialization — rejected for v1; the pluggable `ISerializer` interface allows opt-in later.
- `kafka-dotnet` (Microsoft) — less mature, smaller community; Confluent is the de facto standard.

---

## R2: RabbitMQ.Client .NET — Adapter Design Best Practices

**Decision**: Use `RabbitMQ.Client` v7+ (async-first API) with `IConnection` / `IChannel` (formerly `IModel`).

**Rationale**:
- RabbitMQ.Client v7 (targeting .NET 8+, compatible with .NET 10) is fully async: `IChannel.BasicPublishAsync`, `IChannel.BasicConsumeAsync`, `IChannel.BasicAckAsync`.
- `IConnection` is thread-safe and long-lived; `IChannel` is NOT thread-safe — use one channel per consumer/publisher or pool channels with a `Channel<T>` dispatch pattern.
- For publishing: `BasicPublishAsync` with `mandatory: true` and publisher confirms (`ConfirmSelectAsync`) to detect unroutable messages → map to `PublishFailure`.
- For consuming: `AsyncEventingBasicConsumer` with `ReceivedAsync` event. Prefetch via `BasicQosAsync(prefetchCount)` maps to FR-019.
- Manual ack: `BasicAckAsync(deliveryTag, multiple: false)` after handler success → at-least-once (FR-021). `BasicNackAsync(deliveryTag, requeue: false)` on exhausted retries → dead-letter exchange routing.
- Dead-letter routing: relies on RabbitMQ's built-in DLX configuration (broker-side); the adapter sends nack and the broker routes to DLX per queue policy (FR-010).
- Connection recovery: `AutomaticRecoveryEnabled = true` on `ConnectionFactory` for resilience.
- `Destination` mapping: message type → exchange + routing key. Queues are consumer-side; exchanges are publish targets.

**Alternatives considered**:
- EasyNetQ — higher-level abstraction, but hides too much control needed for the adapter contract (prefetch, manual ack, DLX).
- MassTransit — full-featured bus; but Messaggero IS the bus, so this would be a competing abstraction layer.

---

## R3: .NET 10 / C# 13 Language Features

**Decision**: Target `net10.0` TFM; leverage C# 13 features where they improve clarity.

**Rationale**:
- .NET 10 is the current STS release (March 2026). Target `net10.0` as the sole TFM for simplicity since the user specified .NET 10.
- C# 13 features to leverage:
  - `params ReadOnlySpan<T>` for builder overloads accepting multiple routing rules.
  - `field` keyword (semi-auto properties) for cleaner model types.
  - Extension types (if available) for optional fluent builder extensions.
  - `Lock` type (`System.Threading.Lock`) for low-contention synchronization in routing table.
- `System.Text.Json` source generators for the default JSON serializer — AOT-friendly, zero-reflection.
- `Microsoft.Extensions.DependencyInjection` integration: provide `IServiceCollection.AddMessaggero(Action<MessagingBuilder>)` extension method.
- `Microsoft.Extensions.Hosting` integration: `MessagingHost` implements `IHostedService` for ASP.NET Core / Generic Host lifecycle.
- `Microsoft.Extensions.Logging` for structured logging (FR-011); `ILogger<T>` injection.

**Alternatives considered**:
- Multi-target `net8.0` + `net10.0` — rejected; user specified .NET 10. Single TFM keeps dependencies simpler.
- Serilog as primary logging — rejected; `ILogger` abstraction lets consumers choose their provider.

---

## R4: NuGet Package Strategy

**Decision**: Four NuGet packages: `Messaggero`, `Messaggero.Kafka`, `Messaggero.RabbitMQ`, `Messaggero.Testing`.

**Rationale**:
- **Messaggero** (core): Zero broker-specific dependencies. Contains abstractions, builder, routing, host, error model, default JSON serializer. This is the package application developers reference.
- **Messaggero.Kafka**: References `Confluent.Kafka` + `Messaggero`. Provides `builder.AddKafka(...)` extension method.
- **Messaggero.RabbitMQ**: References `RabbitMQ.Client` + `Messaggero`. Provides `builder.AddRabbitMQ(...)` extension method.
- **Messaggero.Testing**: References `Messaggero` only. Provides `InMemoryTransportAdapter` + `TestMessageBus` for unit/contract tests without broker infrastructure (NFR-005).
- Separation ensures consumers don't pull Confluent.Kafka when they only use RabbitMQ, and vice versa.
- All packages share the same version number, published together.

**Alternatives considered**:
- Single mega-package with all adapters — rejected; forces unnecessary dependency bloat.
- Adapter-only packages without a core package — rejected; needs a shared abstraction layer.

---

## R5: Fluent Builder API Design

**Decision**: Single `MessagingBuilder` entry point with typed extension methods per adapter, method chaining, and build-time validation.

**Rationale**:
- Pattern: `services.AddMessaggero(builder => builder.AddKafka(...).AddRabbitMQ(...).Route<OrderPlaced>(r => r.ToTransport("kafka")).RegisterHandler<OrderHandler>().Build())`.
- Builder accumulates registrations; `Build()` validates: (1) all routed transports exist, (2) no duplicate handler registrations for same type+transport, (3) retry policies reference valid dead-letter destinations.
- Validation errors are thrown as `MessagingConfigurationException` at build time (before host starts) — per spec edge case #9.
- Builder produces an immutable `MessagingConfiguration` consumed by `MessagingHost`.
- Each adapter package provides extension methods: `AddKafka(string name, Action<KafkaOptions>)`, `AddRabbitMQ(string name, Action<RabbitMqOptions>)`.
- Named transports via string key (e.g., `"kafka"`, `"rabbitmq"`) for routing rules and scoped handler registration (US6).

**Alternatives considered**:
- Attribute-based routing (e.g., `[RouteTo("kafka")]` on message classes) — rejected; mixes routing policy into domain models, violating spec requirement that routing is builder-only (FR-005).
- Configuration file (JSON/YAML) based routing — rejected for v1; fluent builder is the primary mechanism. Environment variable defaults supported per FR-017.

---

## R6: Retry and Dead-Letter Strategy

**Decision**: Library-level retry loop with configurable policy per transport; broker-native DLX for dead-lettering.

**Rationale**:
- Retry is handled in the library's consumer dispatch loop, NOT delegated to broker-native retry. This ensures consistent behavior across adapters.
- `RetryPolicy`: `MaxAttempts` (default 3), `BackoffStrategy` (Fixed or Exponential), `InitialDelay`, `MaxDelay`, `RetryableExceptions` (filter).
- After exhausting retries: nack the message (RabbitMQ → DLX routes it) or produce to a dead-letter topic (Kafka → explicit produce to DLT).
- Kafka: no native DLX — library publishes failed messages to a configured `DeadLetterTopic`. This is explicit in the adapter.
- RabbitMQ: leverage broker-native DLX via nack with `requeue: false`, provided the queue is configured with `x-dead-letter-exchange`. Adapter documents this prerequisite.
- `RetryExhaustedException` emitted to observability pipeline after final failure.

**Alternatives considered**:
- Broker-native retry (Kafka retry topic pattern) — rejected; inconsistent across brokers and harder to configure uniformly.
- Polly for retry — considered; rejected because the retry loop is tightly integrated with message dispatch and ack/nack semantics. A simple custom retry is more appropriate than adding Polly as a dependency on the core package.

---

## R7: Serialization Architecture

**Decision**: `IMessageSerializer` interface with `Serialize<T>` / `Deserialize<T>` methods; `System.Text.Json` default implementation; configurable per adapter via builder.

**Rationale**:
- Interface: `byte[] Serialize<T>(T message, MessageHeaders headers)` / `T Deserialize<T>(ReadOnlySpan<byte> data, MessageHeaders headers)`. Headers carry content-type for polymorphic deserialization.
- Default: `JsonMessageSerializer` using `System.Text.Json` with source generators for AOT compatibility. `JsonSerializerOptions.UnknownTypeHandling = JsonUnknownTypeHandling.JsonNode` for tolerant reader (FR-018).
- Per-adapter override: `builder.AddKafka("kafka", opts => opts.UseSerializer(new ProtobufSerializer()))`.
- Deserialization failure: catch `JsonException` → wrap in `DeserializationException` → route to dead-letter (spec edge case #4).

**Alternatives considered**:
- `Newtonsoft.Json` as default — rejected; `System.Text.Json` is the platform default, faster, and AOT-friendly.
- Global serializer only — rejected; per-adapter serializers allow mixed formats (e.g., Avro on Kafka, JSON on RabbitMQ).

---

## R8: Observability Design

**Decision**: `ILogger` for structured logs; `System.Diagnostics.Metrics` for metrics; `System.Diagnostics.ActivitySource` for distributed tracing.

**Rationale**:
- Structured logging via `ILogger` with well-defined event IDs and message templates. Log fields: message ID, message type, transport name, destination, latency, outcome, retry attempt (FR-011).
- Metrics via `System.Diagnostics.Metrics` (OpenTelemetry-compatible natively in .NET): counters for messages published/consumed/retried/dead-lettered, histograms for latency.
- Distributed tracing via `ActivitySource` — create `Activity` per publish/consume operation with transport/routing tags.
- No sensitive data: payloads and credentials excluded from all log/metric/trace emissions (FR-013).
- Observability is opt-in via builder: `builder.EnableObservability()` configures default meters and activity sources.

**Alternatives considered**:
- OpenTelemetry SDK as a hard dependency — rejected; `System.Diagnostics` is the built-in .NET instrumentation API and is natively collected by OpenTelemetry exporters without forcing the SDK on consumers.
- Custom metrics interface — rejected; `System.Diagnostics.Metrics` is the .NET standard and interoperates with all major APM tools.

---

## R9: Handler Concurrency and Backpressure

**Decision**: `SemaphoreSlim`-based concurrency limiter per handler; `Channel<T>` buffer per adapter consumer with bounded capacity.

**Rationale**:
- Default: sequential (concurrency = 1). Configurable via `builder.RegisterHandler<T>(opts => opts.MaxConcurrency(5))`.
- Consumer loop reads from broker → writes to bounded `Channel<T>` (capacity = prefetch limit, FR-019). When channel is full, consumer pauses polling.
- Dispatch loop reads from channel → acquires `SemaphoreSlim` → invokes handler. When semaphore is saturated (concurrency limit reached), dispatch loop blocks, creating natural backpressure.
- This decouples broker fetch rate from handler processing rate and respects both prefetch and concurrency limits.

**Alternatives considered**:
- `TaskScheduler` with `MaxDegreeOfParallelism` — less explicit control over the pipeline stages.
- `System.Threading.Channels.UnboundedChannel` — rejected; unbounded defeats backpressure intent.

---

## R10: Testing Without Brokers

**Decision**: `InMemoryTransportAdapter` in `Messaggero.Testing` package implements full `ITransportAdapter` contract for test scenarios.

**Rationale**:
- `InMemoryTransportAdapter` stores published messages in a `ConcurrentQueue<Message>` and dispatches to registered handlers synchronously (or via configurable delay for async simulation).
- Supports all adapter contract methods: publish, subscribe, ack, nack, lifecycle.
- `TestMessageBus` provides assertion helpers: `AssertPublished<T>()`, `AssertHandled<T>()`, `GetPublishedMessages()`.
- Contract tests parameterize over `ITransportAdapter` — run same test suite against InMemory, Kafka (Testcontainers), and RabbitMQ (Testcontainers) to verify uniform behavior (NFR-005).
- Integration tests use `Testcontainers.Kafka` and `Testcontainers.RabbitMQ` for real broker testing in CI.

**Alternatives considered**:
- Mocking `ITransportAdapter` in each test — works for unit tests but doesn't validate contract compliance across adapters.
- Docker Compose for CI — Testcontainers is more portable and self-contained per test run.
