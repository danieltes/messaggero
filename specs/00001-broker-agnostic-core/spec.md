# Feature Specification: Broker-Agnostic Messaging Library with Multi-Transport Routing

**Feature Branch**: `00001-broker-agnostic-core`  
**Created**: 2026-03-29  
**Status**: Approved  
**Input**: User description: "Broker-Agnostic Messaging Library with Multi-Transport Routing"

## Problem Statement

Applications that integrate directly with a specific message broker (e.g., Kafka, RabbitMQ)
become tightly coupled to its API, configuration model, and delivery semantics. This makes
it costly to switch brokers, test in isolation, or leverage multiple brokers concurrently
for different message types. Furthermore, broker-selection logic leaks into application code,
creating an unnecessary concern that should belong to the infrastructure layer.

A broker-agnostic messaging library eliminates this coupling by exposing:

1. A single, stable interface for publishing and consuming messages.
2. Swappable, simultaneously active transport adapters underneath.
3. A message-type-based routing layer that decides which transport handles each message,
   freeing application code from that responsibility entirely.
4. A fluent configuration API that lets developers compose the full messaging topology —
   transports, routing rules, handler registrations, and policies — in a single,
   readable builder chain.

**Intended outcomes**:
- Application code is decoupled from broker specifics.
- Multiple transports can be configured and run concurrently within the same process
  through a single fluent builder.
- Routing policy is configuration-driven and centrally owned by the library.
- Switching or adding a broker costs zero application-code changes.
- Message handlers are self-contained classes with clear lifecycle hooks, making them
  independently testable and reusable.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Publish a message to a single transport (Priority: P1)

A developer configures one transport adapter via the fluent builder and calls
`publish(message)`. The library delivers the message to the correct broker without the
developer specifying which transport to use.

**Why this priority**: Foundation of the library. All other stories build on it.

**Independent Test**: Use the fluent builder to register a Kafka adapter as the sole
transport. Publish a message. Verify it arrives on the configured Kafka topic without
specifying the adapter explicitly. Repeat with a RabbitMQ adapter only.

**Acceptance Scenarios**:

1. **Given** only a Kafka adapter is registered via the fluent builder, **When**
   `publish(message)` is called, **Then** the message is delivered to the Kafka topic
   mapped to that message type.
2. **Given** only a RabbitMQ adapter is registered via the fluent builder, **When**
   `publish(message)` is called, **Then** the message is delivered to the RabbitMQ
   exchange/queue mapped to that message type.
3. **Given** the broker is unavailable at publish time, **When** `publish(message)` is
   called, **Then** a typed `PublishFailure` error is returned and the message is not
   silently dropped.

---

### User Story 2 — Subscribe to a single transport with a class-based handler (Priority: P1)

A developer implements a handler class for a message type and registers it through the
fluent builder. The library delivers matching messages from the configured transport to
that handler's processing method.

**Why this priority**: Core consumption capability; together with User Story 1 forms the
minimum viable library.

**Independent Test**: Implement a handler class for a message type. Register it via the
fluent builder on a Kafka adapter. Publish a message directly via the broker. Verify the
handler's processing method is invoked with the correct payload.

**Acceptance Scenarios**:

1. **Given** a handler class is registered for a message type on Kafka, **When** a matching
   message arrives, **Then** the handler's processing method is invoked exactly once with the
   correct payload.
2. **Given** the handler's processing method throws an error, **When** a message is received,
   **Then** the library retries according to the configured retry policy.
3. **Given** the retry limit is exhausted, **When** a message cannot be processed, **Then** it
   is routed to the configured dead-letter destination and a `RetryExhaustedException` error is emitted.
4. **Given** a handler class defines lifecycle hooks (e.g., initialize, dispose), **When** the
   library starts or shuts down, **Then** the corresponding hooks are invoked in order.

---

### User Story 3 — Configure and run multiple transports simultaneously (Priority: P2)

A developer uses a single fluent builder chain to register a Kafka adapter and a RabbitMQ
adapter at the same time. Both are active concurrently within the same process.

**Why this priority**: Core multi-transport premise. Necessary before routing can function.

**Independent Test**: Use the fluent builder to register both adapters in one configuration
chain. Publish two messages of different types — one routed to Kafka, one to RabbitMQ.
Verify both arrive on the correct broker independently, without affecting each other.

**Acceptance Scenarios**:

1. **Given** both Kafka and RabbitMQ adapters are registered via the fluent builder, **When**
   each adapter's connection is established, **Then** both are active and independently
   operable.
2. **Given** one broker becomes unavailable, **When** a message is published to the other
   broker's transport, **Then** that publish succeeds independently and a typed error is
   emitted only for the failing transport.
3. **Given** both adapters are active, **When** the library is shut down gracefully,
   **Then** both adapters are drained and closed without message loss.
4. **Given** a developer needs to add a third transport later, **When** they extend the
   fluent builder chain with the new adapter, **Then** no existing publish or subscribe
   code changes are required.

---

### User Story 4 — Route messages to transports by message type (Priority: P2)

The library decides which transport adapter handles a given message based on a
message-type-to-transport routing rule declared in the fluent builder, with no routing
code in application logic.

**Why this priority**: Central value proposition — removes broker-selection from
application code.

**Independent Test**: Define a routing policy in the fluent builder where
`OrderPlaced` → Kafka, `EmailRequested` → RabbitMQ. Publish both types using identical
`publish(message)` calls. Verify `OrderPlaced` arrives only on Kafka and `EmailRequested`
arrives only on RabbitMQ.

**Acceptance Scenarios**:

1. **Given** a routing policy maps `OrderPlaced` to Kafka and `EmailRequested` to
   RabbitMQ, **When** `publish(orderPlacedMessage)` is called, **Then** it is delivered
   via the Kafka adapter only.
2. **Given** the same routing policy, **When** `publish(emailRequestedMessage)` is called,
   **Then** it is delivered via the RabbitMQ adapter only.
3. **Given** no routing rule exists for a message type, **When** `publish(message)` is
   called, **Then** a typed `NoRouteFoundException` error is returned — not a silent no-op.
4. **Given** a routing policy maps a message type to multiple transports (fan-out),
   **When** `publish(message)` is called, **Then** the message is delivered to all mapped
   transports; per-transport failures are reported individually.

---

### User Story 5 — Subscribe to the same message type on multiple transports simultaneously (Priority: P2)

A developer registers a handler class for a message type once through the fluent builder.
The library fans in messages from all registered transports that carry that message type,
delivering them to the same handler instance.

**Why this priority**: Removes the need for per-broker subscription wiring in application
code.

**Independent Test**: Register a single handler class via the fluent builder. Publish one
message directly to the Kafka topic and one directly to the RabbitMQ queue for the same
message type. Verify the same handler is invoked twice — once per message — without any
per-broker code in the application.

**Acceptance Scenarios**:

1. **Given** a handler class is registered for `OrderPlaced` and both Kafka and RabbitMQ
   adapters are active, **When** a message arrives on either broker, **Then** the handler's
   processing method is invoked with the correct payload regardless of source.
2. **Given** a subscription is active on both transports, **When** one broker's connection
   is lost, **Then** consumption continues from the available broker and a `TransportDegradedException`
   event is emitted.
3. **Given** the same message arrives on both transports (e.g., due to a dual-publish),
   **When** the handler is invoked, **Then** the message source transport is available in the
   message metadata for deduplication purposes.

---

### User Story 6 — Subscribe to a specific transport explicitly (Priority: P3)

A developer explicitly scopes a handler class registration to a single named transport in
the fluent builder, overriding the default all-active-transports behavior.

**Why this priority**: Escape hatch for advanced use cases; not needed for core operation.

**Independent Test**: With both adapters active, register a handler class scoped to Kafka
only via the fluent builder. Publish to RabbitMQ directly. Verify the handler is NOT
invoked.

**Acceptance Scenarios**:

1. **Given** both adapters are active and a handler class is scoped to Kafka, **When** a
   matching message arrives on RabbitMQ, **Then** the handler is NOT invoked.
2. **Given** a transport-scoped handler registration, **When** the named transport is not
   registered, **Then** a typed `TransportNotFoundException` error is returned at registration time.

---

### User Story 7 — Observe and diagnose message flows (Priority: P3)

An operator monitors throughput, latency, routing decisions, error rates, and retry counts
through structured logs, metrics, and traces.

**Why this priority**: Required for production readiness; can be deferred from initial
milestone.

**Independent Test**: Enable observability via the fluent builder. Publish 100 messages
routed across both adapters. Verify each log entry includes: message ID, message type,
resolved transport, destination, latency ms, and outcome.

**Acceptance Scenarios**:

1. **Given** observability is enabled, **When** a message is published, **Then** a structured
   log entry records: message ID, message type, resolved transport name, destination, and
   latency.
2. **Given** a routing decision is made, **When** observability is enabled, **Then** the
   routing rule that matched and the selected transport are recorded.
3. **Given** a message fails and is retried, **When** observability is enabled, **Then** each
   retry attempt is logged with attempt number, transport name, and error reason.

---

### Edge Cases

- What happens when a message type is published and no routing rule matches?
  → A typed `NoRouteFoundException` error is returned immediately; the message is not silently dropped.
- What happens when a routing rule maps a message type to a transport that is not currently
  registered or is offline?
  → A `TransportNotFoundException` error at build-time if the adapter was never registered; a `PublishFailure` at runtime if the adapter is offline.
- What happens when a fan-out publish partially succeeds (some transports succeed, others
  fail)?
  → `PublishResult` contains per-transport outcomes; successful deliveries are not rolled back, and each failure is reported individually.
- What happens when deserialization of a received message fails?
  → A typed `DeserializationException` is emitted, the message is routed to the dead-letter destination, and the handler is not invoked.
- How are ordering guarantees communicated when they differ per adapter (e.g., Kafka
  per-partition order vs. RabbitMQ queue order)?
  → Each adapter documents its ordering guarantees. The library does not impose cross-transport ordering; adapter-level documentation is the source of truth.
- What happens when the same consumer group is subscribed to both transports for the same
  message type — how is deduplication handled?
  → The library exposes source-transport metadata on consumed messages; application-level deduplication is the consumer's responsibility.
- What happens when a message exceeds a broker's maximum payload size?
  → The adapter rejects the publish with a `PublishFailure` error containing the broker's size constraint details.
- What happens when a handler class's lifecycle hook (initialize or dispose) throws an
  error?
  → The error is surfaced as a typed exception during host startup/shutdown; the host does not silently swallow lifecycle failures.
- What happens when the fluent builder receives conflicting configuration (e.g., the same
  message type routed to a transport that was not registered)?
  → Build-time validation fails with a descriptive configuration error before the host starts.
- What happens when a handler class is registered for a message type that has no
  corresponding routing rule?
  → Build-time validation emits a warning; the handler is registered but will never receive messages until a matching routing rule is added.

---

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST expose a broker-agnostic asynchronous `publish(message)` operation that
  resolves the target transport(s) internally based on message type. The operation MUST return
  an async result (future/promise/task) that resolves to a `PublishResult` on broker acknowledgement
  or a typed failure.
- **FR-002**: The library MUST support class-based message handlers. Each handler class
  MUST define a processing method that receives the message. Handler classes MAY define
  lifecycle hooks for initialization and disposal. The library MUST manage handler
  lifecycle in coordination with transport start/stop.
- **FR-003**: The library MUST support simultaneous registration and operation of multiple
  transport adapters within the same process, configured through a single fluent builder
  chain. Initial required adapters: Kafka, RabbitMQ.
- **FR-004**: The library MUST implement a message-type-to-transport routing layer that
  resolves which adapter(s) handle each message type at publish time.
- **FR-005**: Routing rules MUST be declaratively configurable through the fluent builder
  API with no routing logic required in application code.
- **FR-006**: When a routing rule maps a message type to multiple transports (fan-out), the
  library MUST publish to all mapped transports and report per-transport outcomes individually.
- **FR-007**: Handler classes MUST receive messages from all active transports by default
  (fan-in), with an explicit opt-in during registration to scope a handler to a named
  transport only.
- **FR-008**: The library MUST expose a typed error model for all failure modes:
  `PublishFailure`, `NoRouteFoundException`, `TransportNotFoundException`,
  `TransportDegradedException`, `DeserializationException`, `RetryExhaustedException`.
- **FR-009**: The library MUST support a configurable retry policy per transport:
  `maxAttempts`, `backoffStrategy` (fixed, exponential), `retryableErrors`,
  `deadLetterDestination`.
- **FR-010**: The library MUST route messages that exhaust retries to a configured dead-letter
  destination.
- **FR-011**: The library MUST emit structured logs and metrics for: message published,
  message consumed, routing decision made, retry attempted, dead-letter routed, transport
  connection lifecycle.
- **FR-012**: Each adapter MUST document its delivery semantics (at-most-once, at-least-once,
  exactly-once where supported), ordering guarantees, and any semantic differences from
  the shared contract.
- **FR-013**: The library MUST NOT log message payloads, credentials, or secrets at any log
  level.
- **FR-014**: The library MUST expose a fluent builder API as the primary configuration
  mechanism. The builder MUST support: registering multiple transport adapters, defining
  routing rules, registering handler classes, configuring retry policies, and enabling
  observability — all within a single composable chain. Builder state MUST be validated
  at build time and MUST report configuration errors before the messaging host starts.
- **FR-015**: The library MUST expose message source-transport metadata on consumed messages
  to enable application-level deduplication when fan-in is active.
- **FR-016**: Handler classes MUST be independently unit-testable without requiring the
  library host or a running broker.
- **FR-017**: Adapter configuration MUST support environment-variable-based defaults in
  addition to fluent builder values.
- **FR-018**: The library MUST define a serializer/deserializer interface. Each adapter
  MUST implement this interface and ship with a JSON default. Adapters MAY be configured
  with an alternative serializer (e.g., Protobuf, Avro) via the fluent builder.
  Deserializers MUST ignore unknown fields by default (tolerant reader) to support
  backward-compatible schema evolution. Deserialization failures MUST surface as a typed
  `DeserializationException`.
- **FR-019**: Each adapter MUST support a configurable prefetch/buffer limit that controls
  the maximum number of unprocessed messages held in memory. When the buffer is full, the
  adapter MUST pause fetching from the broker until handlers drain capacity. The prefetch
  limit MUST be configurable per transport via the fluent builder.
- **FR-020**: The library MUST support configurable handler concurrency. The default MUST
  be sequential (concurrency = 1, one message at a time per handler). A maximum concurrency
  level MUST be configurable per handler registration via the fluent builder. When
  concurrency > 1, the library MAY invoke the handler's processing method concurrently
  up to the configured limit.
- **FR-021**: The library MUST use automatic post-completion acknowledgement as the default.
  A message MUST be acknowledged to the broker only after the handler's processing method
  completes successfully. On handler failure, the message MUST NOT be acknowledged and
  MUST be retried according to the configured retry policy. This provides at-least-once
  delivery semantics by default.

### Non-Functional Requirements

- **NFR-001**: Switching, adding, or removing a transport adapter MUST require zero changes
  to application-level publish calls or handler class implementations.
- **NFR-002**: The per-message routing resolution overhead (excluding broker network latency)
  MUST be ≤ 0.5 ms p99 for a routing table of ≤ 1,000 rules.
- **NFR-003**: Publish p99 latency overhead introduced by the library (excluding broker
  network latency) MUST be ≤ 1 ms for payloads ≤ 1 MB.
- **NFR-004**: The library MUST sustain ≥ 10,000 messages/second aggregate throughput with
  two adapters active simultaneously under benchmark conditions.
- **NFR-005**: The library MUST be fully testable without a running broker via an in-memory
  test adapter that satisfies the full broker contract.
- **NFR-006**: A single adapter failure MUST NOT crash or block message flow on other active
  adapters.

### Key Entities

- **Message**: envelope transmitted through the library — `id` (unique, library-generated
  before routing; caller MAY override), `type` (string, used for routing), `payload`
  (opaque bytes or structured), `headers` (key-value metadata), `timestamp`,
  `sourceTransport` (populated on consumed messages).
- **MessageType**: a string identifier that links a message to routing rules and handler
  class registrations. The primary key for all routing decisions.
- **Handler**: a class that processes messages of a given type. Defines a processing method
  invoked per message and optional lifecycle hooks (initialize, dispose). Independently
  testable; managed by the library's host lifecycle. Concurrency is configurable per
  handler (default: sequential, concurrency = 1).
- **Destination**: broker-agnostic reference to a topic, queue, or exchange. Resolves to
  broker-specific constructs per adapter.
- **Adapter**: implementation of the broker transport contract for a specific broker —
  responsible for transport, connection lifecycle, and error mapping. Each adapter owns
  its serializer (implementing a library-defined serializer/deserializer interface; JSON
  default) and MAY be configured with an alternative serializer via the fluent builder.
- **RoutingRule**: maps a `MessageType` to one or more named `Adapter` registrations, with
  optional overrides for destination and delivery options.
- **RoutingTable**: the keyed collection of `RoutingRule`s (dictionary-based O(1) lookup by
  message type) consulted at publish time to resolve target transports.
- **RetryPolicy**: per-adapter configuration — `maxAttempts`, `backoffStrategy`,
  `retryableErrors`, `deadLetterDestination`.
- **PublishResult**: typed outcome of a publish operation — success with per-transport broker
  acknowledgement metadata, or a structured failure per transport.
- **MessagingBuilder**: the fluent configuration entry point. Composes transports, routing
  rules, handler class registrations, retry policies, and observability settings into a
  validated, immutable host configuration. Validated at build time.
- **MessagingHost**: the runtime created by the builder. Manages adapter lifecycles, handler
  lifecycles, and message dispatch.

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application publish calls and handler class implementations are identical
  across single-transport and multi-transport configurations in all integration tests —
  zero broker-specific branches in application logic.
- **SC-002**: A routing policy that maps `OrderPlaced` → Kafka and `EmailRequested` →
  RabbitMQ routes correctly in 100% of end-to-end test cases with both adapters active.
- **SC-003**: A fan-out routing rule delivers a message to both Kafka and RabbitMQ in 100%
  of test cases; individual adapter failures report per-transport errors without suppressing
  the other adapter's result.
- **SC-004**: Routing resolution overhead is ≤ 0.5 ms p99 for a 1,000-rule routing table,
  verified by the benchmark suite.
- **SC-005**: Publish p99 latency overhead (library only, excluding network) is ≤ 1 ms for
  payloads ≤ 1 MB, verified by the benchmark suite.
- **SC-006**: Aggregate throughput of ≥ 10,000 msg/s is sustained with Kafka and RabbitMQ
  adapters simultaneously active under load test conditions.
- **SC-007**: Failure of one broker adapter does not interrupt message flow on the other
  adapter, verified by a chaos/fault-injection integration test.
- **SC-008**: 100% of reliability-critical paths (retry, dead-letter, connection failure,
  partial fan-out failure) are covered by automated tests.
- **SC-009**: A developer can publish and consume a message with two active transports and
  a routing policy within 20 minutes of reading the quickstart guide.
- **SC-010**: Zero sensitive fields (payload content, credentials, tokens) appear in
  library-emitted logs at any level, verified by a log-scrubbing test.
- **SC-011**: Handler classes can be instantiated and unit-tested in isolation without the
  library host, verified by dedicated unit test examples.

## Clarifications

### Session 2026-03-29

- Q: Is `publish(message)` synchronous (blocking) or asynchronous (returns future/promise/task)? → A: Asynchronous — `publish` returns an async result that resolves on broker acknowledgement or failure.
- Q: Who generates the Message `id` — library, caller, or broker? → A: Library-generated (unique ID assigned before routing); caller MAY override with a custom ID.
- Q: Where does serialization live — global library-level, per-adapter, or per-message-type? → A: Adapter-owned — each adapter defines its serializer via a library-provided interface; JSON default per adapter.
- Q: How does the library manage consumer backpressure when handlers cannot keep up? → A: Prefetch-limited — configurable per-transport buffer size; adapter pauses fetching when buffer is full.
- Q: How does the library handle message schema evolution (e.g., new fields added by producer)? → A: Tolerant reader — deserializers ignore unknown fields by default; no library-level schema versioning.
- Q: Are handler processing methods invoked concurrently or sequentially? → A: Configurable — default sequential (concurrency = 1); max concurrency per handler configurable via builder.
- Q: When is a consumed message acknowledged to the broker? → A: Auto post-completion — library acks after handler succeeds; nacks and retries on failure (at-least-once default).

## Assumptions

- The library is consumed as a package/dependency by application developers; it is not a standalone service.
- Kafka and RabbitMQ are the two initial broker targets; additional adapters (e.g., Azure Service Bus, Amazon SQS) are out of scope for v1 but the adapter contract is designed to be extensible.
- Message serialization format (e.g., JSON, Protobuf) is pluggable per adapter; JSON is the default.
- The library runs within a single process; distributed coordination across multiple library instances is the broker's responsibility (e.g., Kafka consumer groups).
- Developers using the library have access to broker infrastructure for integration testing; the in-memory test adapter covers unit and contract testing without a broker.
- Transport-level authentication and TLS configuration are delegated to adapter-specific settings; the library does not implement its own credential store.
- Ordering guarantees are adapter-specific and documented per adapter; the library does not attempt cross-transport ordering.
- Dead-letter destinations are pre-provisioned on the broker; the library routes to them but does not auto-create them.
- Schema evolution follows a tolerant-reader pattern: deserializers ignore unknown fields by default. Schema registry integration and explicit message-type versioning are out of scope for v1.
