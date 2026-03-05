# Feature Specification: Message Bus Abstraction Library

**Feature Branch**: `001-message-bus-abstraction`  
**Created**: 2026-03-04  
**Status**: Draft  
**Input**: User description: "Build a library that can help me make use of different message buses like RabbitMQ and Kafka. I should be able to switch between different transports with minimal overhead"

## Clarifications

### Session 2026-03-04

- Q: What delivery guarantee does the library promise? → A: At-least-once — message is acknowledged only after successful handler execution; duplicates possible.
- Q: Should the library automatically reconnect after connection loss? → A: Yes — auto-reconnect with configurable exponential backoff (initial delay, multiplier, max delay, max attempts).
- Q: What message ordering guarantee does the library provide? → A: Per-key ordering — messages with the same routing/partition key are delivered in order; no ordering guarantee across keys.
- Q: How many messages can a subscriber process concurrently? → A: Configurable concurrency limit per subscription (default 1); library enforces per-key sequential processing within that limit.
- Q: How does the library handle competing consumers for horizontal scaling? → A: Consumer group required — each subscription specifies a group ID; instances in the same group compete for messages; different groups receive independent copies.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Publish Messages Through a Unified Interface (Priority: P1)

A developer integrating the library into a service wants to publish messages to a topic or queue without coupling to a specific message bus vendor. They configure a transport (e.g., RabbitMQ), obtain a publisher from the library, and send messages using the same method signature regardless of the underlying transport. If the team later decides to migrate to Kafka, only the transport configuration changes — no application code is modified.

**Why this priority**: Publishing is the most fundamental operation of any messaging library. Without a working, transport-agnostic publish path, no other feature (subscribing, switching transports) delivers value.

**Independent Test**: Can be fully tested by configuring a transport, publishing a message, and verifying it arrives at the broker. Delivers immediate value: the developer can send messages today and swap transports tomorrow.

**Acceptance Scenarios**:

1. **Given** the library is configured with a RabbitMQ transport, **When** the developer publishes a message to a named destination, **Then** the message is delivered to RabbitMQ and can be read from the corresponding queue.
2. **Given** the library is configured with a Kafka transport, **When** the developer publishes the same message using the same publish call, **Then** the message is delivered to the corresponding Kafka topic.
3. **Given** a valid transport configuration, **When** the developer publishes a message with a payload, headers, and a routing key/partition key, **Then** all metadata is faithfully forwarded to the underlying broker.
4. **Given** the broker is temporarily unreachable, **When** the developer publishes a message, **Then** the library returns a clear error indicating delivery failure without crashing or hanging indefinitely.
5. **Given** the broker connection is lost after initial establishment, **When** the library detects the disconnection, **Then** it automatically attempts reconnection using exponential backoff and emits lifecycle events throughout the process.

---

### User Story 2 - Subscribe to Messages Through a Unified Interface (Priority: P2)

A developer wants to consume messages from a queue or topic by registering a message handler. The handler receives deserialized messages through a common envelope regardless of which transport is active. The subscription manages its own lifecycle (connect, consume, disconnect) behind the scenes.

**Why this priority**: Consuming messages is the second half of the core messaging contract. Combined with US1, this completes the fundamental publish-subscribe loop that makes the library usable end-to-end.

**Independent Test**: Can be tested by subscribing to a destination, publishing a message (via US1), and asserting the handler receives the expected envelope with correct payload and metadata.

**Acceptance Scenarios**:

1. **Given** a RabbitMQ transport is configured, **When** the developer registers a handler for a named destination, **Then** the handler is invoked for each message arriving on that destination.
2. **Given** a Kafka transport is configured, **When** the developer registers the same handler using the same subscription call, **Then** the handler is invoked for each message on the corresponding topic.
3. **Given** a handler is registered, **When** a message arrives, **Then** the handler receives a transport-agnostic envelope containing the payload, headers, timestamp, and source destination.
4. **Given** two service instances subscribe to the same destination with the same consumer group ID, **When** a message is published, **Then** exactly one instance receives and processes the message.
5. **Given** the handler throws an error during processing, **When** the error occurs, **Then** the library does not silently drop the message and instead follows a configurable error strategy (e.g., retry, dead-letter, or reject).
6. **Given** a subscription is active, **When** the developer requests a graceful shutdown, **Then** the library finishes processing any in-flight messages and cleanly disconnects from the broker.

---

### User Story 3 - Switch Transports with Configuration-Only Change (Priority: P3)

A team running services in production on RabbitMQ decides to migrate to Kafka (or vice versa). The developer changes only the transport configuration — no application code, handler signatures, or message contracts need modification. The library validates the new configuration on startup and connects to the new broker transparently.

**Why this priority**: Transport portability is the key differentiator of this library. While it depends on US1 and US2 being functional, proving that a swap is truly configuration-only validates the entire abstraction.

**Independent Test**: Can be tested by running the same integration test suite once with a RabbitMQ transport config and once with a Kafka transport config, asserting identical behavior from the application's perspective.

**Acceptance Scenarios**:

1. **Given** a working application configured with RabbitMQ, **When** the developer changes only the transport configuration to Kafka, **Then** the application starts successfully and publishes/consumes messages via Kafka without code changes.
2. **Given** an invalid transport configuration, **When** the application starts, **Then** the library fails fast with a clear, actionable validation error before any broker connection is attempted.
3. **Given** the library supports two transports, **When** the developer requests the list of available transports, **Then** each transport is self-describing (name, required configuration keys, connection health status).

---

### User Story 4 - Observe and Debug Message Flow (Priority: P4)

An operations engineer investigating a message-delivery issue needs visibility into what is happening inside the library. The library emits structured, transport-agnostic lifecycle events (connected, disconnected, message published, message received, error) that the engineer can feed into existing observability tooling.

**Why this priority**: While not part of the core publish/subscribe path, observability is essential for production readiness and aligns with the Messaggero constitution's observability requirements (Principle IV).

**Independent Test**: Can be tested by hooking a test observer to the library, performing a publish or subscribe operation, and asserting that the expected lifecycle events are emitted with correct metadata.

**Acceptance Scenarios**:

1. **Given** the library is configured with an event listener, **When** a message is successfully published, **Then** a "message.published" event is emitted containing destination, message ID, and timestamp.
2. **Given** the library is configured with an event listener, **When** a connection to the broker is lost and re-established, **Then** "transport.disconnected" and "transport.connected" events are emitted in order.
3. **Given** the library is configured with an event listener, **When** message consumption fails, **Then** a "message.error" event is emitted containing the error details, the message envelope, and the destination.

---

### Edge Cases

- What happens when the developer attempts to publish before the transport connection is established? The library MUST either queue the message until connected (with a configurable buffer limit) or return an explicit error — never silently discard.
- What happens when the broker enforces a maximum message size and the payload exceeds it? The library MUST reject the message before sending and return a size-limit error with the broker's limit and the actual payload size.
- What happens when two subscribers register for the same destination? The library MUST support multiple concurrent handlers per destination and deliver each message to all registered handlers.
- How does the library handle serialization failures (e.g., a payload that cannot be converted to the configured wire format)? It MUST reject the publish call with a serialization error and not send a malformed message to the broker.
- What happens when a transport is configured but its underlying broker client dependency is not available at runtime? The library MUST fail fast at initialization with a clear dependency-missing error.
- What happens when a message is delivered more than once due to at-least-once semantics (e.g., handler succeeded but ack failed)? The library MUST deliver the message again to the handler. Consumers are responsible for idempotent processing; the library SHOULD document this contract clearly.
- What happens when auto-reconnect exhausts all retry attempts? The library MUST emit a terminal "transport.failed" lifecycle event with the last error and stop retrying. The application can observe this event and decide whether to shut down or attempt a manual reconnect.
- What happens when concurrency is set above 1 and multiple messages arrive with the same routing key? The library MUST queue same-key messages and process them sequentially even while other keys are processed in parallel, ensuring per-key ordering is never violated.
- What happens when a consumer group has only one active instance and that instance goes offline? The broker retains unacknowledged messages. When a new instance joins the same group, it MUST resume consuming from where the previous instance left off (subject to broker retention policies).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The library MUST define a transport-agnostic publish interface that accepts a destination name, a message payload, and optional metadata (headers, routing/partition key).
- **FR-002**: The library MUST define a transport-agnostic subscribe interface that accepts a destination name, a consumer group ID, and a message handler, and delivers messages via a common envelope type.
- **FR-003**: The library MUST provide at least two transport implementations: one for RabbitMQ and one for Kafka.
- **FR-004**: The library MUST allow transport selection and configuration via a structured configuration object provided at initialization time. Changing the transport MUST NOT require changes to application code.
- **FR-005**: The message envelope MUST contain at minimum: payload (bytes or deserialized object), headers (key-value map), source destination name, message ID, and timestamp.
- **FR-006**: The library MUST support configurable serialization/deserialization of message payloads. JSON MUST be supported out of the box; additional formats (e.g., Protobuf, Avro, MessagePack) SHOULD be pluggable.
- **FR-007**: The library MUST validate transport configuration at startup and fail fast with actionable errors if required settings are missing or invalid.
- **FR-008**: The library MUST provide a graceful shutdown mechanism that finishes processing in-flight messages before disconnecting from the broker.
- **FR-009**: The library MUST support configurable error-handling strategies for failed message processing, including at minimum: retry (with configurable count and backoff), dead-letter (forward to a designated destination), and reject (discard and log).
- **FR-010**: The library MUST expose lifecycle events (connected, disconnected, message published, message received, message error) via an observable/event-listener mechanism.
- **FR-011**: The library MUST support multiple concurrent handlers for the same destination.
- **FR-012**: The library MUST be safe for concurrent use from multiple threads or asynchronous tasks without external synchronization.
- **FR-013**: The library MUST support health-check queries that report the current connection status of the active transport.
- **FR-014**: The library MUST provide at-least-once delivery semantics by default. Messages MUST NOT be acknowledged to the broker until the handler has completed successfully. Consumers MUST be prepared to receive duplicate messages; the library SHOULD document this expectation and MAY provide optional idempotency helpers.
- **FR-015**: The library MUST automatically reconnect to the broker after an unexpected connection loss using exponential backoff. The backoff strategy MUST be configurable: initial delay, multiplier, maximum delay, and maximum retry attempts. During reconnection, the library MUST emit lifecycle events (transport.disconnected, transport.reconnecting, transport.connected) and MUST resume subscriptions automatically once reconnected.
- **FR-016**: The library MUST guarantee that messages published with the same routing/partition key are delivered to handlers in publish order. No ordering guarantee is made across different keys. This MUST be documented as a contract for consumers.
- **FR-017**: The library MUST support a configurable concurrency limit per subscription, controlling how many messages are processed in parallel for a given destination. The default MUST be 1 (sequential processing). When concurrency is greater than 1, the library MUST still enforce per-key sequential delivery — only messages with different routing/partition keys may be processed concurrently.
- **FR-018**: The library MUST require a consumer group identifier for every subscription. Instances sharing the same group ID MUST compete for messages (each message delivered to exactly one instance in the group). Instances with different group IDs MUST receive independent copies of every message. The group ID maps to Kafka consumer groups and RabbitMQ queue names.

### Key Entities

- **Transport**: Represents a message bus backend (e.g., RabbitMQ, Kafka). Encapsulates connection management, protocol translation, and broker-specific behavior. Selected via configuration.
- **Publisher**: Responsible for sending messages to a named destination via the active transport. Accepts a destination name, payload, and optional metadata.
- **Subscriber**: Responsible for receiving messages from a named destination and dispatching them to registered handlers. Manages its own connection lifecycle. Supports a configurable concurrency limit that controls how many messages are processed in parallel, while preserving per-key ordering. Requires a consumer group ID to enable competing-consumer semantics for horizontal scaling.
- **Message Envelope**: A transport-agnostic container for a single message. Contains payload, headers, message ID, timestamp, source destination, and routing/partition key. The routing/partition key determines ordering scope: messages sharing the same key are delivered in order.
- **Transport Configuration**: A structured object that describes which transport to use and all broker-specific settings (host, port, credentials, connection pool size, timeouts, reconnection backoff parameters, etc.).
- **Error Strategy**: A policy applied when a message handler fails. Defines behavior such as retry count/backoff, dead-letter destination, or reject-and-log.
- **Lifecycle Event**: A structured notification emitted by the library at key moments (connect, disconnect, publish, receive, error) for observability purposes.

## Assumptions

- The library is intended for use within the Messaggero ecosystem but is designed as a standalone, independently consumable package.
- Initial transport support is limited to RabbitMQ and Kafka. Additional transports (e.g., Amazon SQS, Azure Service Bus, Redis Streams) can be added later by implementing the transport interface.
- JSON is the default serialization format. Custom serializers are pluggable but the library does not ship with Protobuf or Avro implementations in the initial release.
- The library does not manage broker infrastructure (e.g., creating queues, topics, or exchanges). It assumes the necessary broker resources already exist or are auto-created by broker-side policies.
- Connection credentials are provided via configuration; the library does not manage secrets or integrate with vault systems directly.
- The library targets server-side use. Mobile or browser clients are out of scope.
- The library provides at-least-once delivery semantics. Exactly-once and at-most-once are not supported in the initial release. Consumers must handle duplicate messages idempotently.
- The library guarantees message ordering only within the scope of a single routing/partition key. Consumers requiring total ordering across all messages on a destination must use a single key (accepting reduced throughput).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can publish and consume a message through the library using a single transport in under 15 minutes of setup time (including reading documentation).
- **SC-002**: Switching from one transport to another requires changing only configuration — zero application-code modifications. Verified by running the same test suite against both transports.
- **SC-003**: The library sustains at least 10,000 messages per second throughput on a single node for both publish and consume paths, matching the Messaggero constitution's performance targets.
- **SC-004**: End-to-end message delivery latency (publish to handler invocation) is under 50 ms at p95 when the broker is co-located, excluding network transit to the broker itself.
- **SC-005**: All public interfaces have documented contracts with examples, enabling a new developer to integrate without reading source code.
- **SC-006**: The library achieves ≥ 90% line coverage with deterministic, non-flaky tests across both transport implementations.
- **SC-007**: Adding a new transport implementation requires implementing only the transport interface — no modifications to core library code, publisher, subscriber, or envelope types.
