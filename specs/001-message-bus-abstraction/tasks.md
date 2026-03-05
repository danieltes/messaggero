# Tasks: Message Bus Abstraction Library

**Input**: Design documents from `/specs/001-message-bus-abstraction/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: Included — the Messaggero constitution (Principle II) mandates test-first development as NON-NEGOTIABLE. Tests MUST be written before or alongside implementation.

**Organization**: Tasks are grouped by user story (from spec.md) to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel with adjacent [P] tasks (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to ([US1], [US2], [US3], [US4])
- Paths are relative to repository root; project structure per plan.md

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create solution structure, configure build tooling, and establish all project scaffolding

- [X] T001 Create Messaggero.sln and directory structure with all 8 projects: src/Messaggero.Abstractions/, src/Messaggero/, src/Messaggero.Transport.RabbitMQ/, src/Messaggero.Transport.Kafka/, tests/Messaggero.Tests.Unit/, tests/Messaggero.Tests.Contract/, tests/Messaggero.Tests.Integration/, tests/Messaggero.Tests.Performance/
- [X] T002 [P] Configure Directory.Build.props with TreatWarningsAsErrors, GenerateDocumentationFile, CS1591 warning-as-error, Roslyn analyzers (CA1502 cyclomatic complexity), and .NET 10 target framework
- [X] T003 [P] Add NuGet package references: Confluent.Kafka to Transport.Kafka, RabbitMQ.Client to Transport.RabbitMQ, System.Text.Json + Microsoft.Extensions.DependencyInjection.Abstractions + Microsoft.Extensions.Logging.Abstractions to Core, xUnit + FluentAssertions + NSubstitute to Unit/Contract tests, Testcontainers.RabbitMq + Testcontainers.Kafka to Integration tests, BenchmarkDotNet to Performance tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Define all abstractions, core DTOs, serializer, DI framework, and test infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create message DTOs per contracts/public-api.md: MessageEnvelope.cs (sealed record), MessageMetadata.cs (sealed record), MessagePublishOptions.cs (sealed record) in src/Messaggero.Abstractions/
- [X] T005 [P] Create subscription types per contracts/public-api.md: SubscriptionOptions.cs (sealed record), ISubscriptionHandle.cs (interface), ITransportSubscription.cs (interface) in src/Messaggero.Abstractions/
- [X] T006 [P] Create ErrorStrategy.cs (sealed record with Retry/DeadLetter/Reject factory methods) and ErrorStrategyType enum per contracts/public-api.md in src/Messaggero.Abstractions/ErrorStrategy.cs
- [X] T007 [P] Create observability types per contracts/public-api.md: LifecycleEvent.cs (sealed record), LifecycleEventType enum, HealthCheckResult.cs (sealed record), ReconnectionOptions.cs (sealed record with validation) in src/Messaggero.Abstractions/
- [X] T008 [P] Create IMessageSerializer.cs (ContentType, Serialize<T>, Deserialize<T>) and IMessageHandler.cs (HandleAsync with MessageEnvelope<T>) interfaces in src/Messaggero.Abstractions/
- [X] T009 Create IMessageBusTransport.cs (ConnectAsync, DisconnectAsync, PublishAsync with ReadOnlyMemory<byte>, SubscribeAsync, CheckHealthAsync, OnLifecycleEvent) and IMessageBus.cs (PublishAsync<T>, SubscribeAsync<T>, CheckHealthAsync, OnLifecycleEvent) interfaces per contracts/public-api.md in src/Messaggero.Abstractions/
- [X] T010 [P] Implement JsonMessageSerializer using System.Text.Json with source generator support in src/Messaggero/Serialization/JsonMessageSerializer.cs — must implement IMessageSerializer, ContentType = "application/json"
- [X] T011 [P] Create transport configuration classes per data-model.md: RabbitMqConfiguration.cs (HostName, Port, UserName, Password, VirtualHost, PublishChannelPoolSize, HeartbeatInterval, NetworkRecoveryInterval, UseSsl) in src/Messaggero.Transport.RabbitMQ/ and KafkaConfiguration.cs (BootstrapServers, Acks, EnableIdempotence, CompressionType, BatchSize, LingerMs, SessionTimeoutMs, HeartbeatIntervalMs, SecurityProtocol) in src/Messaggero.Transport.Kafka/
- [X] T012 Create MessageBusBuilder.cs (UseTransport<T>, UseSerializer, WithReconnection) and ServiceCollectionExtensions.cs (AddMessaggero) per contracts/public-api.md in src/Messaggero/ — include a minimal MessageBus.cs skeleton class implementing IMessageBus with NotImplementedException stubs
- [X] T013 [P] Create Testcontainers collection fixtures: RabbitMqFixture.cs (RabbitMqBuilder with rabbitmq:3-management image) and KafkaFixture.cs (KafkaBuilder with confluentinc/cp-kafka image) with random port binding per research.md R4 in tests/Messaggero.Tests.Integration/Fixtures/
- [X] T014 [P] Write unit tests for MessageEnvelope<T> validation rules (non-empty MessageId, destination format, timestamp default, ContentType match) in tests/Messaggero.Tests.Unit/MessageEnvelopeTests.cs
- [X] T015 [P] Write unit tests for JsonMessageSerializer (serialize/deserialize round-trip, complex types, null handling, ContentType property) in tests/Messaggero.Tests.Unit/JsonMessageSerializerTests.cs

**Checkpoint**: All abstractions compiled, serializer tested, DI framework in place — user story implementation can begin

---

## Phase 3: User Story 1 — Publish Messages Through a Unified Interface (Priority: P1) 🎯 MVP

**Goal**: A developer can publish messages to RabbitMQ or Kafka using a single `IMessageBus.PublishAsync<T>()` call. Metadata (headers, routing key, correlation ID) is faithfully forwarded. Broker errors surface as clear exceptions.

**Independent Test**: Configure a transport, publish a message, verify it arrives at the broker. Swap transport config and repeat — same code, different broker.

### Tests for User Story 1

> **Constitution Principle II**: Write these tests FIRST, ensure they FAIL before implementation

- [X] T016 [P] [US1] Write unit tests for MessageBus publish orchestration (serialization → envelope creation → transport.PublishAsync delegation, null destination rejection, CancellationToken forwarding) using NSubstitute mock of IMessageBusTransport in tests/Messaggero.Tests.Unit/MessageBusPublishTests.cs
- [X] T017 [P] [US1] Write publisher contract tests verifying IMessageBusTransport.PublishAsync contract (destination propagation, body serialization fidelity, metadata mapping, routing key forwarding) parameterized for future transport implementations in tests/Messaggero.Tests.Contract/PublisherContractTests.cs

### Implementation for User Story 1

- [X] T018 [US1] Implement MessageBus.PublishAsync<T> in src/Messaggero/MessageBus.cs — serialize payload via IMessageSerializer, construct MessageMetadata (generate MessageId via UUID v7, set Timestamp, map options), delegate to IMessageBusTransport.PublishAsync with ReadOnlyMemory<byte> body
- [X] T019 [P] [US1] Implement RabbitMqTransport.cs (connection management with two IConnection instances per research.md R2: publish connection + consume connection, AutomaticRecoveryEnabled, TopologyRecoveryEnabled) and RabbitMqPublisher.cs (channel pool of PublishChannelPoolSize, ConfirmSelectAsync per channel, BasicPublishAsync with publisher confirms, fanout exchange declaration) in src/Messaggero.Transport.RabbitMQ/
- [X] T020 [P] [US1] Implement KafkaTransport.cs (single thread-safe IProducer<string, byte[]> instance per research.md R1, idempotent producer config) and KafkaPublisher.cs (ProduceAsync with delivery report, Confluent ISerializer<byte[]> adapter wrapping IMessageSerializer) in src/Messaggero.Transport.Kafka/
- [X] T021 [P] [US1] Create UseRabbitMq DI extension method in src/Messaggero.Transport.RabbitMQ/ServiceCollectionExtensions.cs — register RabbitMqTransport as IMessageBusTransport, accept Action<RabbitMqConfiguration>
- [X] T022 [P] [US1] Create UseKafka DI extension method in src/Messaggero.Transport.Kafka/ServiceCollectionExtensions.cs — register KafkaTransport as IMessageBusTransport, accept Action<KafkaConfiguration>
- [X] T023 [P] [US1] Write RabbitMQ publish integration tests using RabbitMqFixture: publish message and verify arrival on broker queue, verify metadata fidelity (headers, routing key, correlation ID), verify broker-unreachable error handling in tests/Messaggero.Tests.Integration/RabbitMqIntegrationTests.cs
- [X] T024 [P] [US1] Write Kafka publish integration tests using KafkaFixture: publish message and verify arrival on topic, verify partition key mapping, verify broker-unreachable error handling in tests/Messaggero.Tests.Integration/KafkaIntegrationTests.cs

**Checkpoint**: User Story 1 fully functional — publish to either broker via `IMessageBus.PublishAsync<T>()`, swap transport with config-only change

---

## Phase 4: User Story 2 — Subscribe to Messages Through a Unified Interface (Priority: P2)

**Goal**: A developer registers an `IMessageHandler<T>` for a destination and receives deserialized `MessageEnvelope<T>` objects. Consumer groups enable competing consumers. Per-key ordering is preserved even with concurrency > 1. Error strategies (retry, dead-letter, reject) are configurable. Graceful shutdown completes in-flight messages.

**Independent Test**: Subscribe to a destination, publish a message (via US1), assert the handler receives the correct envelope with payload and metadata.

### Tests for User Story 2

> **Constitution Principle II**: Write these tests FIRST, ensure they FAIL before implementation

- [X] T025 [P] [US2] Write unit tests for KeyPartitionedProcessor: same-key messages processed sequentially, different-key messages processed concurrently, null-key round-robin, partition count equals concurrency limit, MurmurHash distribution per research.md R6 in tests/Messaggero.Tests.Unit/KeyPartitionedProcessorTests.cs
- [X] T026 [P] [US2] Write unit tests for MessageBus subscribe path (handler registration, envelope deserialization, ISubscriptionHandle lifecycle, DisposeAsync graceful shutdown, multiple handlers per destination per FR-011) using NSubstitute in tests/Messaggero.Tests.Unit/MessageBusSubscribeTests.cs
- [X] T027 [P] [US2] Write subscriber contract tests verifying IMessageBusTransport.SubscribeAsync contract (handler invocation, envelope completeness, unsubscribe via ITransportSubscription.DisposeAsync) in tests/Messaggero.Tests.Contract/SubscriberContractTests.cs

### Implementation for User Story 2

- [X] T028 [US2] Implement KeyPartitionedProcessor in src/Messaggero/Concurrency/KeyPartitionedProcessor.cs — ConcurrentDictionary of per-partition Channel<T> queues, partition index = MurmurHash(key) % concurrencyLimit, null-key round-robin, long-running consumer task per partition per research.md R6
- [X] T029 [US2] Implement MessageBus.SubscribeAsync<T> and error handling in src/Messaggero/MessageBus.cs — deserialize incoming ReadOnlyMemory<byte> via IMessageSerializer, construct MessageEnvelope<T>, dispatch through KeyPartitionedProcessor, implement ErrorStrategy (retry with exponential backoff, dead-letter forwarding, reject with nack), implement DisposeAsync for graceful shutdown (drain in-flight, disconnect)
- [X] T030 [P] [US2] Implement RabbitMqSubscriber in src/Messaggero.Transport.RabbitMQ/RabbitMqSubscriber.cs — one channel per subscription on consume connection, ConsumerDispatchConcurrency=1 per research.md R2, BasicConsumeAsync, manual BasicAckAsync after handler success, destination mapping: exchange={destination}, queue={destination}.{groupId} per research.md R2
- [X] T031 [P] [US2] Implement KafkaSubscriber in src/Messaggero.Transport.Kafka/KafkaSubscriber.cs — one IConsumer<string, byte[]> per subscription (NOT thread-safe per research.md R1), dedicated consume loop task, EnableAutoCommit=false, manual StoreOffset + Commit after handler, group.id from SubscriptionOptions.GroupId
- [X] T032 [P] [US2] Write RabbitMQ subscribe integration tests using RabbitMqFixture: publish-then-consume round-trip, consumer group competing consumers (two subscribers same group → one receives), error strategy retry, graceful shutdown in tests/Messaggero.Tests.Integration/RabbitMqIntegrationTests.cs
- [X] T033 [P] [US2] Write Kafka subscribe integration tests using KafkaFixture: publish-then-consume round-trip, consumer group competing consumers, per-key ordering with concurrency > 1, graceful shutdown in tests/Messaggero.Tests.Integration/KafkaIntegrationTests.cs

**Checkpoint**: User Stories 1 AND 2 both work — full publish-subscribe loop functional through both transports

---

## Phase 5: User Story 3 — Switch Transports with Configuration-Only Change (Priority: P3)

**Goal**: A team switches from RabbitMQ to Kafka (or vice versa) by changing only the DI registration — no handler code, message contracts, or publish calls change. Invalid configuration fails fast with actionable errors.

**Independent Test**: Run the same integration test suite once with RabbitMQ config and once with Kafka config, asserting identical application-level behavior.

### Tests for User Story 3

- [X] T034 [US3] Write transport contract tests parameterized across both transports (same test logic, different IMessageBusTransport implementation): publish round-trip, subscribe round-trip, metadata fidelity, error strategy behavior in tests/Messaggero.Tests.Contract/TransportContractTests.cs

### Implementation for User Story 3

- [X] T035 [US3] Implement configuration validation with fail-fast errors in src/Messaggero/MessageBusBuilder.cs — validate required fields (RabbitMq: HostName, Kafka: BootstrapServers), port ranges (1-65535), pool sizes (≥ 1), emit actionable error messages per FR-007 and data-model.md validation rules
- [X] T036 [US3] Add transport self-description to RabbitMqTransport.cs and KafkaTransport.cs — Name property, required configuration keys, connection health status per US3 acceptance scenario 3
- [X] T037 [US3] Write transport switch integration tests: run identical publish-subscribe scenario with RabbitMQ config then Kafka config asserting identical behavior, test invalid config fail-fast, test transport name and self-description in tests/Messaggero.Tests.Integration/TransportSwitchTests.cs

**Checkpoint**: All three core user stories verified — transport switching is truly configuration-only

---

## Phase 6: User Story 4 — Observe and Debug Message Flow (Priority: P4)

**Goal**: The library emits structured lifecycle events (connected, disconnected, reconnecting, failed, published, received, error) via `OnLifecycleEvent`. Health checks report transport connection status. Auto-reconnect uses exponential backoff with configurable parameters.

**Independent Test**: Hook a test observer, perform publish/subscribe operations, assert expected lifecycle events are emitted with correct metadata (event type, timestamp, transport name, destination, message ID).

### Tests for User Story 4

- [X] T038 [P] [US4] Write unit tests for ExponentialBackoffReconnector: initial delay, multiplier, max delay cap, max attempts exhaustion → TransportFailed event, cancellation support in tests/Messaggero.Tests.Unit/ExponentialBackoffReconnectorTests.cs
- [X] T039 [P] [US4] Write unit tests for lifecycle event emission: MessagePublished emitted on publish, MessageReceived on consume, MessageError on handler failure, TransportConnected/Disconnected on connect/disconnect, listener registration and disposal in tests/Messaggero.Tests.Unit/LifecycleEventEmissionTests.cs

### Implementation for User Story 4

- [X] T040 [US4] Implement ExponentialBackoffReconnector in src/Messaggero/Resilience/ExponentialBackoffReconnector.cs — configurable InitialDelay, Multiplier, MaxDelay, MaxAttempts per ReconnectionOptions; emit TransportReconnecting events during attempts; emit TransportFailed on exhaustion per edge case 7; integrate with RabbitMQ transport (Kafka handles reconnection internally per research.md R1)
- [X] T041 [US4] Implement OnLifecycleEvent listener registration and lifecycle event emission in MessageBus.cs and both transports — emit MessagePublished after successful publish, MessageReceived before handler dispatch, MessageError on handler failure, TransportConnected/Disconnected/Reconnecting from transport layer per FR-010 and LifecycleEventType enum
- [X] T042 [US4] Implement CheckHealthAsync in src/Messaggero/MessageBus.cs (delegates to transport), src/Messaggero.Transport.RabbitMQ/RabbitMqTransport.cs (check IConnection.IsOpen), src/Messaggero.Transport.Kafka/KafkaTransport.cs (check producer handle status) per FR-013 and HealthCheckResult contract
- [X] T043 [US4] Write lifecycle and health check integration tests: verify TransportConnected on startup, MessagePublished after publish, MessageReceived on consume, TransportDisconnected + TransportReconnecting on broker restart, CheckHealthAsync returns healthy/unhealthy correctly in tests/Messaggero.Tests.Integration/LifecycleIntegrationTests.cs

**Checkpoint**: All four user stories complete — full observability suite operational

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Performance benchmarks, documentation completeness, code quality validation

- [X] T044 [P] Create publish performance benchmarks targeting ≥ 10k msg/s: measure throughput, p95 latency, memory allocations per message using BenchmarkDotNet --job short --exporters json --memory per research.md R4 in tests/Messaggero.Tests.Performance/PublishBenchmark.cs
- [X] T045 [P] Create consume performance benchmarks targeting ≥ 10k msg/s: measure throughput, p95 latency, KeyPartitionedProcessor overhead, memory allocations using BenchmarkDotNet in tests/Messaggero.Tests.Performance/ConsumeBenchmark.cs
- [X] T046 Add XML doc-comments to all public API types in src/Messaggero.Abstractions/ and src/Messaggero/ — verify CS1591 compliance (zero missing doc-comment warnings), document purpose, parameters, return values, error conditions per Constitution Principle I
- [X] T047 Run quickstart.md validation checklist: start app with broker, publish message, confirm handler receives envelope, stop app confirming graceful shutdown, switch transport config confirming identical behavior
- [X] T048 Code cleanup and final validation: remove all TODOs, verify zero analyzer warnings (dotnet format --verify-no-changes), verify test coverage ≥ 90% overall and ≥ 95% on critical paths (publish/subscribe/error handling), run full test suite

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2) — no other story dependencies
- **User Story 2 (Phase 4)**: Depends on Foundational (Phase 2) — uses US1's publish path for integration tests but can be implemented in parallel
- **User Story 3 (Phase 5)**: Depends on US1 and US2 being complete (validates switching across both publish and subscribe paths)
- **User Story 4 (Phase 6)**: Depends on US1 and US2 (lifecycle events are emitted from existing publish/subscribe paths). Can overlap with US3.
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Foundational only → **MVP candidate** — delivers immediate value
- **US2 (P2)**: Foundational only (uses US1 publish path in integration tests but implementation is independent)
- **US3 (P3)**: Requires US1 + US2 complete (validates full publish-subscribe across both transports)
- **US4 (P4)**: Requires US1 + US2 complete (emits events from publish/subscribe paths)

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (Constitution Principle II)
2. Core orchestration (MessageBus) before transport-specific implementations
3. Transport implementations (RabbitMQ, Kafka) can run in parallel
4. DI extensions after their respective transport implementation
5. Integration tests after all components in the story are implemented

---

## Parallel Opportunities

### Phase 2: Foundational

```
Parallel batch 1 (all independent DTOs/interfaces):
  T004 (message DTOs) | T005 (subscription types) | T006 (ErrorStrategy) |
  T007 (observability types) | T008 (IMessageSerializer + IMessageHandler)

Sequential: T009 (IMessageBusTransport + IMessageBus — depends on all DTOs)

Parallel batch 2 (implementations):
  T010 (JsonMessageSerializer) | T011 (transport configs) |
  T013 (Testcontainers fixtures) | T014 (envelope tests) | T015 (serializer tests)

Sequential: T012 (MessageBusBuilder + DI — depends on interfaces)
```

### Phase 3: User Story 1 (Publish)

```
Parallel batch 1 (tests first):
  T016 (unit tests) | T017 (contract tests)

Sequential: T018 (MessageBus.PublishAsync)

Parallel batch 2 (transports):
  T019 (RabbitMQ transport+publisher) | T020 (Kafka transport+publisher)

Parallel batch 3 (DI extensions):
  T021 (UseRabbitMq) | T022 (UseKafka)

Parallel batch 4 (integration tests):
  T023 (RabbitMQ integration) | T024 (Kafka integration)
```

### Phase 4: User Story 2 (Subscribe)

```
Parallel batch 1 (tests first):
  T025 (KeyPartitionedProcessor tests) | T026 (subscribe unit tests) | T027 (contract tests)

Sequential: T028 (KeyPartitionedProcessor) → T029 (MessageBus.SubscribeAsync)

Parallel batch 2 (transport subscribers):
  T030 (RabbitMqSubscriber) | T031 (KafkaSubscriber)

Parallel batch 3 (integration tests):
  T032 (RabbitMQ subscribe integration) | T033 (Kafka subscribe integration)
```

### Phase 6: User Story 4 (Observe & Debug)

```
Parallel batch 1 (tests first):
  T038 (reconnector tests) | T039 (lifecycle event tests)

Sequential: T040 (ExponentialBackoffReconnector) → T041 (lifecycle events) → T042 (health checks)

Sequential: T043 (integration tests — requires all above)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks all stories)
3. Complete Phase 3: User Story 1 (Publish)
4. **STOP and VALIDATE**: Publish messages to both RabbitMQ and Kafka, swap config
5. Deploy/demo if ready — developer can already send messages

### Incremental Delivery

1. Setup + Foundational → Compilable project with all interfaces
2. **Add US1 (Publish)** → Test independently → **MVP!** Developer can publish messages
3. **Add US2 (Subscribe)** → Test independently → Full publish-subscribe loop
4. **Add US3 (Transport Switch)** → Validate config-only switching → Portability proven
5. **Add US4 (Observe & Debug)** → Lifecycle events + health checks → Production-ready
6. **Polish** → Benchmarks + docs + cleanup → Release candidate

### Parallel Team Strategy

With multiple developers after Foundational phase:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - **Developer A**: User Story 1 (Publish) — RabbitMQ transport path
   - **Developer B**: User Story 1 (Publish) — Kafka transport path
   - After US1 is done, same split for User Story 2 (Subscribe)
3. US3 and US4 can overlap once US1 + US2 are complete

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same batch
- [Story] label maps task to specific user story for traceability
- All file paths reference the project structure defined in plan.md
- Contracts in contracts/public-api.md are the authoritative source for interface signatures
- Research.md R1-R6 contain transport-specific implementation patterns and decisions
- Data-model.md contains validation rules for all DTOs
- Constitution Principle II mandates tests before implementation — this is enforced in task ordering
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
