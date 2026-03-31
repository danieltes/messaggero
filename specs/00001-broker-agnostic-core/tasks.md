# Tasks: Broker-Agnostic Messaging Library with Multi-Transport Routing

**Input**: Design documents from `/specs/00001-broker-agnostic-core/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included per user story per Constitution Art. III. Contract tests follow the 8-case adapter matrix from adapter-contract.md.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution structure, project files, NuGet dependencies

- [x] T001 Create solution file (Messaggero.sln), Directory.Build.props with net10.0 TFM and shared properties, and directory structure for src/ and tests/ at repository root
- [x] T002 [P] Configure src project files with NuGet dependencies: src/Messaggero/Messaggero.csproj (Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, System.Text.Json), src/Messaggero.Kafka/Messaggero.Kafka.csproj (Confluent.Kafka, Messaggero reference), src/Messaggero.RabbitMQ/Messaggero.RabbitMQ.csproj (RabbitMQ.Client, Messaggero reference), src/Messaggero.Testing/Messaggero.Testing.csproj (Messaggero reference)
- [x] T003 [P] Configure test project files with framework dependencies: tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj (xUnit, NSubstitute, FluentAssertions), tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj (xUnit, FluentAssertions), tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj (xUnit, Testcontainers, FluentAssertions), tests/Messaggero.Tests.Benchmarks/Messaggero.Tests.Benchmarks.csproj (BenchmarkDotNet)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions, models, error types, serialization, and builder skeleton that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create core abstractions: IMessageBus in src/Messaggero/Abstractions/IMessageBus.cs, IMessageHandler<T> and IHandlerLifecycle in src/Messaggero/Abstractions/IMessageHandler.cs, ITransportAdapter in src/Messaggero/Abstractions/ITransportAdapter.cs, IMessageSerializer in src/Messaggero/Abstractions/IMessageSerializer.cs, MessageContext in src/Messaggero/Abstractions/MessageContext.cs
- [x] T005 [P] Create core models: Message in src/Messaggero/Model/Message.cs (Id, Type, Payload as ReadOnlyMemory<byte>, Headers, Timestamp, SourceTransport), MessageHeaders in src/Messaggero/Model/MessageHeaders.cs (Dictionary<string, string> wrapper), Destination in src/Messaggero/Model/Destination.cs (Name string)
- [x] T006 [P] Create PublishResult and TransportOutcome models in src/Messaggero/Model/PublishResult.cs (per-transport outcomes with success/failure and broker metadata)
- [x] T007 [P] Create typed error hierarchy in src/Messaggero/Errors/: MessagingException base class, PublishFailure.cs, NoRouteFoundException.cs, TransportNotFoundException.cs, TransportDegradedException.cs, DeserializationException.cs, RetryExhaustedException.cs, MessagingConfigurationException.cs
- [x] T008 [P] Create configuration models in src/Messaggero/Configuration/: TransportOptions.cs, RetryPolicyOptions.cs (MaxAttempts=3, BackoffStrategy, InitialDelay=1s, MaxDelay=30s, RetryableExceptions, DeadLetterDestination), RoutingRuleOptions.cs, HandlerOptions.cs (MaxConcurrency=1), HandlerRegistration.cs, TransportRegistration.cs, BackoffStrategy.cs enum (Fixed, Exponential)
- [x] T009 Create MessagingConfiguration immutable record aggregating RoutingTable, TransportRegistrations, and HandlerRegistrations in src/Messaggero/Configuration/MessagingConfiguration.cs
- [x] T010 Create JsonMessageSerializer using System.Text.Json with source generators (AOT-friendly), tolerant reader (ignore unknown fields), and DeserializationException on failure in src/Messaggero/Serialization/JsonMessageSerializer.cs
- [x] T011 Create MessagingBuilder skeleton with AddTransport registration, build-time validation, and Build() producing immutable MessagingConfiguration in src/Messaggero/Configuration/MessagingBuilder.cs

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Publish a Message to a Single Transport (Priority: P1) 🎯 MVP

**Goal**: A developer configures one transport adapter via the fluent builder and calls PublishAsync(message). The library delivers the message to the correct broker without the developer specifying which transport to use.

**Independent Test**: Register a Kafka adapter as the sole transport. Publish a message. Verify it arrives on the configured Kafka topic. Repeat with RabbitMQ adapter only.

### Implementation for User Story 1

- [x] T012 [US1] Create RoutingRule and RoutingTable with O(1) dictionary-based lookup by message type in src/Messaggero/Routing/RoutingRule.cs and src/Messaggero/Routing/RoutingTable.cs
- [x] T013 [US1] Implement Route<TMessage>(Action<RoutingRuleBuilder>) with RoutingRuleBuilder (ToTransport, ToDestination) in src/Messaggero/Configuration/MessagingBuilder.cs
- [x] T014 [P] [US1] Create KafkaOptions (BootstrapServers, env-var defaults per FR-017) and KafkaBuilderExtensions.AddKafka() in src/Messaggero.Kafka/KafkaOptions.cs and src/Messaggero.Kafka/KafkaBuilderExtensions.cs
- [x] T015 [P] [US1] Create RabbitMqOptions (HostName, Port, env-var defaults per FR-017) and RabbitMqBuilderExtensions.AddRabbitMQ() in src/Messaggero.RabbitMQ/RabbitMqOptions.cs and src/Messaggero.RabbitMQ/RabbitMqBuilderExtensions.cs
- [x] T016 [P] [US1] Implement MessageBus.PublishAsync: resolve routing rule by message type, serialize via adapter's IMessageSerializer, create Message envelope, publish to resolved transport, return PublishResult in src/Messaggero/Hosting/MessageBus.cs
- [x] T017 [P] [US1] Create MessagingHost implementing IHostedService for adapter lifecycle management (StartAsync starts all adapters, StopAsync gracefully stops) in src/Messaggero/Hosting/MessagingHost.cs
- [x] T018 [US1] Create AddMessaggero IServiceCollection extension method registering MessagingBuilder, IMessageBus, and MessagingHost in src/Messaggero/Configuration/MessagingServiceCollectionExtensions.cs
- [x] T019 [P] [US1] Implement KafkaTransportAdapter: StartAsync (create IProducer<string, byte[]>), StopAsync (flush and dispose), PublishAsync (ProduceAsync with DeliveryResult mapping to TransportOutcome) in src/Messaggero.Kafka/KafkaTransportAdapter.cs
- [x] T020 [P] [US1] Implement RabbitMqTransportAdapter: StartAsync (create IConnection + IChannel, enable publisher confirms), StopAsync (close channel/connection), PublishAsync (BasicPublishAsync with confirm mapping to TransportOutcome) in src/Messaggero.RabbitMQ/RabbitMqTransportAdapter.cs
- [x] T021 [P] [US1] Create InMemoryTransportAdapter with publish support (ConcurrentQueue<Message> storage, StartAsync/StopAsync lifecycle) in src/Messaggero.Testing/InMemoryTransportAdapter.cs
- [x] T022 [US1] Create TestMessageBus with assertion helpers (AssertPublished<T>, GetPublishedMessages) in src/Messaggero.Testing/TestMessageBus.cs

### Tests for User Story 1

- [x] T047 [P] [US1] Unit tests for RoutingTable O(1) lookup and NoRouteFoundException in tests/Messaggero.Tests.Unit/Routing/RoutingTableTests.cs
- [x] T048 [P] [US1] Unit tests for MessageBus.PublishAsync routing resolution, serialization, and PublishResult assembly in tests/Messaggero.Tests.Unit/Hosting/MessageBusTests.cs
- [x] T049 [P] [US1] Contract test: publish succeeds → TransportOutcome.Success with metadata (runs against InMemory, Kafka, RabbitMQ adapters) in tests/Messaggero.Tests.Contract/PublishContractTests.cs
- [x] T050 [P] [US1] Contract test: publish to unavailable broker → TransportOutcome with PublishFailure in tests/Messaggero.Tests.Contract/PublishContractTests.cs

**Checkpoint**: Single-transport publish works end-to-end. A developer can configure one adapter, publish a message, and verify delivery.

---

## Phase 4: User Story 2 — Subscribe with a Class-Based Handler (Priority: P1)

**Goal**: A developer implements a handler class for a message type and registers it through the fluent builder. The library delivers matching messages from the configured transport to that handler's processing method, with retry and dead-letter support.

**Independent Test**: Implement a handler for a message type. Register it via the fluent builder on a Kafka adapter. Publish a message directly via the broker. Verify the handler's processing method is invoked with the correct payload.

### Implementation for User Story 2

- [x] T023 [US2] Implement RegisterHandler<THandler, TMessage>(Action<HandlerOptions>) in MessagingBuilder with handler type validation in src/Messaggero/Configuration/MessagingBuilder.cs
- [x] T024 [US2] Implement handler dispatch loop: adapter onMessage → bounded Channel<T> (capacity = prefetch limit) → SemaphoreSlim (MaxConcurrency) → handler invocation → auto-ack on success / nack on failure in src/Messaggero/Hosting/HandlerDispatcher.cs
- [x] T025 [US2] Implement retry execution with configurable backoff (fixed/exponential) and dead-letter routing after retries exhausted in src/Messaggero/Hosting/RetryExecutor.cs
- [x] T026 [US2] Integrate handler lifecycle hooks (IHandlerLifecycle.InitializeAsync/DisposeAsync) with MessagingHost start/stop in src/Messaggero/Hosting/MessagingHost.cs
- [x] T027 [P] [US2] Implement KafkaTransportAdapter: SubscribeAsync (background consume loop with CancellationToken), AcknowledgeAsync (StoreOffset + Commit), RejectAsync (skip commit + produce to dead-letter topic) in src/Messaggero.Kafka/KafkaTransportAdapter.cs
- [x] T028 [P] [US2] Implement RabbitMqTransportAdapter: SubscribeAsync (AsyncEventingBasicConsumer + BasicQosAsync for prefetch), AcknowledgeAsync (BasicAckAsync), RejectAsync (BasicNackAsync with requeue:false for DLX routing) in src/Messaggero.RabbitMQ/RabbitMqTransportAdapter.cs
- [x] T029 [P] [US2] Implement InMemoryTransportAdapter subscribe and ack/nack support with dead-letter list in src/Messaggero.Testing/InMemoryTransportAdapter.cs

### Tests for User Story 2

- [x] T051 [P] [US2] Unit tests for RetryExecutor: fixed backoff, exponential backoff capped at MaxDelay, retry filter, RetryExhaustedException in tests/Messaggero.Tests.Unit/Hosting/RetryExecutorTests.cs
- [x] T052 [P] [US2] Unit tests for HandlerDispatcher: Channel<T> buffering, SemaphoreSlim concurrency limiting, auto-ack/nack in tests/Messaggero.Tests.Unit/Hosting/HandlerDispatcherTests.cs
- [x] T053 [P] [US2] Contract test: subscribe delivers message with correct payload and SourceTransport in tests/Messaggero.Tests.Contract/SubscribeContractTests.cs
- [x] T054 [P] [US2] Contract test: AcknowledgeAsync prevents redelivery; RejectAsync triggers dead-letter routing in tests/Messaggero.Tests.Contract/AckNackContractTests.cs
- [x] T055 [P] [US2] Contract test: prefetch limit pauses consumption when buffer is full in tests/Messaggero.Tests.Contract/BackpressureContractTests.cs
- [x] T056 [US2] Example unit test demonstrating handler instantiated and tested in isolation without library host (SC-011) in tests/Messaggero.Tests.Unit/Examples/HandlerIsolationExampleTests.cs

**Checkpoint**: Full publish-subscribe cycle works. A developer can publish and consume messages with retry and dead-letter handling on a single transport.

---

## Phase 5: User Story 3 — Configure and Run Multiple Transports Simultaneously (Priority: P2)

**Goal**: A developer uses a single fluent builder chain to register a Kafka adapter and a RabbitMQ adapter at the same time. Both are active concurrently within the same process.

**Independent Test**: Register both adapters in one builder chain. Publish two messages of different types — one routed to Kafka, one to RabbitMQ. Verify both arrive on the correct broker independently.

### Implementation for User Story 3

- [x] T030 [US3] Implement concurrent multi-adapter lifecycle in MessagingHost: parallel StartAsync/StopAsync for all registered adapters with graceful drain on shutdown in src/Messaggero/Hosting/MessagingHost.cs
- [x] T031 [US3] Implement adapter failure isolation: per-adapter try-catch in lifecycle and dispatch, emit TransportDegradedException on connection loss without crashing other adapters (NFR-006) in src/Messaggero/Hosting/MessagingHost.cs

### Tests for User Story 3

- [x] T057 [US3] Integration test: adapter failure isolation — kill one adapter connection while the other publishes successfully (SC-007, NFR-006) in tests/Messaggero.Tests.Integration/AdapterIsolationTests.cs
- [x] T058 [US3] Contract test: StopAsync drains in-flight messages without loss in tests/Messaggero.Tests.Contract/LifecycleContractTests.cs

**Checkpoint**: Multiple adapters run concurrently. One adapter failure does not interrupt message flow on other adapters.

---

## Phase 6: User Story 4 — Route Messages to Transports by Message Type (Priority: P2)

**Goal**: The library decides which transport adapter handles a given message based on a message-type-to-transport routing rule declared in the fluent builder, with no routing code in application logic.

**Independent Test**: Define routing: OrderPlaced → Kafka, EmailRequested → RabbitMQ. Publish both types using identical PublishAsync calls. Verify OrderPlaced arrives only on Kafka and EmailRequested arrives only on RabbitMQ.

### Implementation for User Story 4

- [x] T032 [US4] Extend RoutingTable to support multi-transport routing rules (one message type → multiple transports) in src/Messaggero/Routing/RoutingTable.cs
- [x] T033 [US4] Implement fan-out publish: when a routing rule maps to multiple transports, publish to all and aggregate per-transport outcomes in PublishResult in src/Messaggero/Hosting/MessageBus.cs
- [x] T034 [US4] Add build-time validation: verify all routing rule transport references exist, warn on handlers with no matching routing rule, fail on duplicate routing rules in src/Messaggero/Configuration/MessagingBuilder.cs

### Tests for User Story 4

- [x] T059 [P] [US4] Integration test: OrderPlaced → Kafka, EmailRequested → RabbitMQ end-to-end (SC-002) using Testcontainers in tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs
- [x] T060 [P] [US4] Integration test: fan-out partial failure — one adapter down, other succeeds, PublishResult reports both (SC-003) in tests/Messaggero.Tests.Integration/FanOutFailureTests.cs

**Checkpoint**: Message-type routing works across multiple transports. Fan-out delivers to all mapped transports with individual outcome reporting.

---

## Phase 7: User Story 5 — Subscribe to Same Message Type on Multiple Transports (Priority: P2)

**Goal**: A developer registers a handler class once. The library fans in messages from all active transports carrying that message type, delivering them to the same handler. Source transport metadata enables application-level deduplication.

**Independent Test**: Register a single handler. Publish one message to Kafka and one to RabbitMQ for the same type. Verify the handler is invoked twice — once per message — with correct SourceTransport metadata.

### Implementation for User Story 5

- [x] T035 [US5] Implement fan-in: auto-subscribe each handler to all active transports for its message type, merge consumption into handler dispatch with SourceTransport metadata propagation in src/Messaggero/Hosting/HandlerDispatcher.cs

### Tests for User Story 5

- [x] T066 [P] [US5] Integration test: register one handler for OrderPlaced, publish to Kafka and RabbitMQ, verify handler invoked twice with correct SourceTransport metadata per message in tests/Messaggero.Tests.Integration/FanInIntegrationTests.cs
- [x] T067 [US5] Integration test: fan-in with degraded transport — kill one broker connection mid-consumption, verify handler continues receiving from available broker and TransportDegradedException is emitted in tests/Messaggero.Tests.Integration/FanInDegradedTests.cs

**Checkpoint**: Fan-in works. A single handler receives messages from all transports with source metadata for deduplication.

---

## Phase 8: User Story 6 — Subscribe to a Specific Transport Explicitly (Priority: P3)

**Goal**: A developer explicitly scopes a handler class registration to a single named transport, overriding the default fan-in behavior.

**Independent Test**: With both adapters active, register a handler scoped to Kafka only. Publish to RabbitMQ. Verify the handler is NOT invoked.

### Implementation for User Story 6

- [x] T036 [US6] Implement ScopeToTransport(string name) option in HandlerOptions and filter handler subscription to the named transport only in src/Messaggero/Configuration/HandlerOptions.cs and src/Messaggero/Hosting/HandlerDispatcher.cs
- [x] T037 [US6] Add build-time validation: fail with TransportNotFoundException if scoped handler references an unregistered transport name in src/Messaggero/Configuration/MessagingBuilder.cs

### Tests for User Story 6

- [x] T068 [P] [US6] Integration test: both adapters active, handler scoped to Kafka only, publish to RabbitMQ, verify handler NOT invoked in tests/Messaggero.Tests.Integration/ScopedSubscriptionTests.cs
- [x] T069 [US6] Unit test: register handler scoped to unregistered transport name, verify MessagingBuilder.Build() throws TransportNotFoundException in tests/Messaggero.Tests.Unit/Configuration/ScopedHandlerValidationTests.cs

**Checkpoint**: Scoped subscriptions work. A handler receives messages only from its named transport.

---

## Phase 9: User Story 7 — Observe and Diagnose Message Flows (Priority: P3)

**Goal**: An operator monitors throughput, latency, routing decisions, error rates, and retry counts through structured logs, metrics, and distributed traces.

**Independent Test**: Enable observability via the fluent builder. Publish 100 messages routed across both adapters. Verify each log entry includes: message ID, message type, resolved transport, destination, latency ms, and outcome.

### Implementation for User Story 7

- [x] T038 [P] [US7] Create ActivitySource for distributed tracing: Activity per publish and consume operation with transport, message type, and destination tags in src/Messaggero/Observability/MessagingActivitySource.cs
- [x] T039 [P] [US7] Create Metrics using System.Diagnostics.Metrics: counters (messages_published, messages_consumed, messages_retried, messages_dead_lettered) and histograms (publish_duration_ms, consume_duration_ms) in src/Messaggero/Observability/MessagingMetrics.cs
- [x] T040 [US7] Add structured ILogger logging at publish, consume, routing decision, retry attempt, dead-letter routed, and adapter lifecycle events across src/Messaggero/Hosting/ files
- [x] T041 [US7] Implement EnableObservability() on MessagingBuilder to register metrics, tracing, and logging infrastructure in src/Messaggero/Configuration/MessagingBuilder.cs
- [x] T042 [US7] Audit all logging call sites to ensure no message payloads, credentials, or secrets are logged at any level (FR-013) across src/Messaggero/, src/Messaggero.Kafka/, src/Messaggero.RabbitMQ/
- [x] T061 [US7] Automated log-scrubbing test: capture all log output from publish/consume cycle and assert zero payloads, credentials, or tokens appear (SC-010) in tests/Messaggero.Tests.Unit/Observability/LogScrubTests.cs

**Checkpoint**: Full observability pipeline active. Logs, metrics, and traces emitted for all message operations with zero sensitive data exposure.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Packaging, documentation, and final validation

- [x] T043 [P] Configure NuGet package metadata (PackageId, Description, Authors, License, RepositoryUrl) for all four packages in src/Messaggero/, src/Messaggero.Kafka/, src/Messaggero.RabbitMQ/, src/Messaggero.Testing/
- [x] T044 [P] Add XML documentation comments to all public API types and members in src/Messaggero/Abstractions/
- [x] T045 Run quickstart.md validation: execute all 6 progressive scenarios end-to-end to verify developer onboarding path
- [x] T062 [P] Write routing resolution benchmark (1,000-rule table, assert ≤0.5ms p99 — SC-004) in tests/Messaggero.Tests.Benchmarks/RoutingBenchmarks.cs
- [x] T063 [P] Write publish overhead benchmark (payloads ≤1MB, assert ≤1ms p99 — SC-005) in tests/Messaggero.Tests.Benchmarks/PublishBenchmarks.cs
- [x] T064 [P] Write aggregate throughput benchmark (two adapters, assert ≥10k msg/s — SC-006) in tests/Messaggero.Tests.Benchmarks/ThroughputBenchmarks.cs
- [x] T065 [P] Write per-adapter delivery semantics documentation for Kafka, RabbitMQ, and InMemory adapters (FR-012) in docs/adapter-semantics.md
- [x] T046 Code cleanup, consistent formatting, and final review across all src/ projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational (Phase 2)
- **US2 (Phase 4)**: Depends on US1 (Phase 3) — needs publish path and adapter infrastructure
- **US3 (Phase 5)**: Depends on US2 (Phase 4) — needs full single-transport publish + subscribe
- **US4 (Phase 6)**: Depends on US1 (Phase 3) — extends routing and publish logic. Fan-out integration testing (T059, T060) benefits from US3 multi-adapter support.
- **US5 (Phase 7)**: Depends on US2 (Phase 4) and US3 (Phase 5) — needs handler dispatch + multi-adapter support
- **US6 (Phase 8)**: Depends on US5 (Phase 7) — refines fan-in with scoping
- **US7 (Phase 9)**: Depends on US2 (Phase 4) — instruments existing publish/consume/retry paths
- **Polish (Phase 10)**: Depends on all desired user stories being complete

### User Story Dependencies

```
Phase 1 (Setup)
    └── Phase 2 (Foundational) ──── BLOCKS ALL ────┐
                                                     ├── US1 (Publish) ─────┬── US2 (Subscribe) ──┬── US3 (Multi-transport) ── US5 (Fan-in) ── US6 (Scoped)
                                                     │                      │                      │
                                                     │                      └── US7 (Observability) │
                                                     │                                              │
                                                     └── US4 (Routing) ────────────────────────────┘
                                                                                                    └── Phase 10 (Polish)
```

### Within Each User Story

- Models / core logic before adapter-specific implementations
- Builder extensions before adapter implementations
- Core hosting before DI integration
- Adapter implementations (Kafka, RabbitMQ, InMemory) can proceed in parallel

### Parallel Opportunities

**Phase 2 (Foundational)**: T004, T005, T006, T007, T008 — all create independent files, can run in parallel

**Phase 3 (US1)**:
- T014, T015 — Kafka and RabbitMQ options/extensions in parallel
- T016, T017 — MessageBus and MessagingHost in parallel
- T019, T020, T021 — All three adapter publish implementations in parallel

**Phase 4 (US2)**:
- T027, T028, T029 — All three adapter subscribe implementations in parallel

**Phase 6 (US4)**: T032, T033 — Routing extension and fan-out in parallel (different files)

**Phase 9 (US7)**: T038, T039 — ActivitySource and Metrics in parallel (different files)

**Phase 10**: T043, T044 — NuGet metadata and XML docs in parallel

**Cross-story parallelism**: Once US2 completes, US3, US4, and US7 can proceed in parallel (different code paths). US5 must wait for US3.

---

## Parallel Example: User Story 1

```
# Sequential dependencies first:
T012  RoutingRule + RoutingTable

T013  Route<T> in MessagingBuilder

# Then launch parallel adapter options:
T014  KafkaOptions + AddKafka          ← parallel
T015  RabbitMqOptions + AddRabbitMQ    ← parallel

# Then launch parallel core hosting:
T016  MessageBus.PublishAsync          ← parallel
T017  MessagingHost IHostedService     ← parallel

# Then DI integration:
T018  AddMessaggero extension

# Then launch parallel adapter publish implementations:
T019  KafkaTransportAdapter publish    ← parallel
T020  RabbitMqTransportAdapter publish ← parallel
T021  InMemoryTransportAdapter publish ← parallel

# Then TestMessageBus:
T022  TestMessageBus assertion helpers
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks all stories)
3. Complete Phase 3: User Story 1 — Publish to single transport
4. Complete Phase 4: User Story 2 — Subscribe with handler
5. **STOP and VALIDATE**: Full publish-subscribe cycle works end-to-end on a single transport
6. Deploy/demo if ready — this is a functional messaging library

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (Publish) → Test single-transport publish → **Milestone: publish works**
3. US2 (Subscribe) → Test full cycle → **Milestone: MVP complete**
4. US3 (Multi-transport) + US4 (Routing) → Test multi-transport routing → **Milestone: multi-transport works**
5. US5 (Fan-in) + US6 (Scoped) → Test subscription modes → **Milestone: subscription model complete**
6. US7 (Observability) → Test instrumentation → **Milestone: production-ready**
7. Polish → Packaging and validation → **Milestone: release candidate**

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 → User Story 2
   - (After US1) Developer B: User Story 4 (routing)
   - (After US2) Developer C: User Story 3 (multi-transport) → User Story 5 (fan-in) → User Story 6 (scoped)
   - (After US2) Developer D: User Story 7 (observability)
3. Within each story: Kafka, RabbitMQ, and InMemory adapter tasks can be split across developers

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same phase
- [Story] label maps each task to a specific user story for traceability
- Each user story has an independent test criterion — verify before moving on
- Commit after each task or logical group
- Adapter implementations (Kafka, RabbitMQ, InMemory) are always parallelizable within a story
- Environment variable defaults (FR-017) are part of adapter options tasks (T014, T015)
- InMemoryTransportAdapter and TestMessageBus are implementation deliverables (Messaggero.Testing NuGet package), not test tasks
