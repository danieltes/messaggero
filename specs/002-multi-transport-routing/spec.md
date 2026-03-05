# Feature Specification: Multi-Transport Routing

**Feature Branch**: `002-multi-transport-routing`  
**Created**: 2026-03-04  
**Status**: Draft  
**Input**: User description: "Make it possible to use multiple transports simultaneously. I should be able to subscribe to RabbitMQ or to Kafka or to both at the same time. I should be able to publish a message and based on the destination, or the message type, the library should be able to decide which transport to use."

## Clarifications

### Session 2026-03-04

- Q: How should the system handle conflicting routing rules (same destination pattern mapped to two different transports)? → A: Raise an error at startup (fail-fast).
- Q: Should the publisher be able to explicitly override the transport on a per-publish call? → A: No. Routing is always configuration-driven.
- Q: When a transport fails to connect at startup and a message is published to a destination routed to that transport, what should happen? → A: Reject the publish immediately with a descriptive error.
- Q: How should the subscribe API determine which transport to use? → A: Use the same routing rules as publish (destination-based resolution).
- Q: What level of type hierarchy matching should type-based routing support? → A: Exact type + direct class hierarchy (walk base classes, ignore interfaces).

## User Scenarios & Testing

### User Story 1 - Register Multiple Transports (Priority: P1)

As a developer, I want to register multiple transports (e.g., RabbitMQ and Kafka) in a single application so that different parts of my system can communicate over different brokers without running separate message bus instances.

**Why this priority**: This is the foundational capability. Nothing else works until multiple transports can coexist within a single `IMessageBus` instance.

**Independent Test**: Can be fully tested by configuring both a RabbitMQ and a Kafka transport during startup, verifying both connections are established and the bus reports healthy for each transport.

**Acceptance Scenarios**:

1. **Given** an application configuring both RabbitMQ and Kafka transports, **When** the message bus starts, **Then** both transports are connected and operational.
2. **Given** an application configuring two transports, **When** the health check is queried, **Then** health status is reported per transport (e.g., one healthy, one degraded).
3. **Given** an application configuring a single transport, **When** the message bus starts, **Then** behavior is identical to the current single-transport experience (backward compatible).

---

### User Story 2 - Publish to a Specific Transport Based on Destination (Priority: P1)

As a developer, I want to publish a message to a destination and have the library automatically select the correct transport based on configured routing rules, so that I don't need to manually manage which broker handles which destination.

**Why this priority**: Publishing is half of the core messaging contract. Developers need a seamless way to publish without caring about underlying transport selection.

**Independent Test**: Can be fully tested by configuring routing rules that map destination "orders.*" to Kafka and "notifications.*" to RabbitMQ, then publishing to each and verifying messages arrive on the correct broker.

**Acceptance Scenarios**:

1. **Given** a routing rule mapping destination pattern "orders.*" to Kafka, **When** a message is published to "orders.created", **Then** the message is sent via the Kafka transport.
2. **Given** a routing rule mapping destination pattern "notifications.*" to RabbitMQ, **When** a message is published to "notifications.email", **Then** the message is sent via the RabbitMQ transport.
3. **Given** no routing rule matches the destination, **When** a message is published, **Then** the message is sent via the configured default transport.
4. **Given** no routing rule matches and no default transport is configured, **When** a message is published, **Then** a clear error is raised indicating the destination cannot be routed.

---

### User Story 3 - Publish to a Specific Transport Based on Message Type (Priority: P2)

As a developer, I want to configure routing rules based on message type (CLR type), so that all messages of a certain type automatically go to the correct transport regardless of the destination name.

**Why this priority**: Type-based routing is a natural complement to destination-based routing and enables convention-over-configuration patterns (e.g., all event types go to Kafka, all command types go to RabbitMQ).

**Independent Test**: Can be fully tested by configuring a type-based rule mapping `OrderEvent` to Kafka and `SendEmailCommand` to RabbitMQ, then publishing each type and verifying correct transport selection.

**Acceptance Scenarios**:

1. **Given** a routing rule mapping message type `OrderEvent` to Kafka, **When** an `OrderEvent` is published to any destination, **Then** the message is sent via the Kafka transport.
2. **Given** both a destination-based rule and a type-based rule could match, **When** a message is published, **Then** the destination-based rule takes precedence (more specific wins).
3. **Given** a type-based rule mapping a base class to a transport, **When** a derived type is published, **Then** the derived type matches the rule for its base class (class hierarchy is walked up to but not including `object`; interfaces are not considered).

---

### User Story 4 - Subscribe Across Transports (Priority: P1)

As a developer, I want to subscribe to messages on a specific transport so that I can consume from RabbitMQ, Kafka, or both simultaneously in the same application.

**Why this priority**: Subscribing is the other half of the core messaging contract. Applications that bridge two brokers must be able to consume from both.

**Independent Test**: Can be fully tested by subscribing to a Kafka topic and a RabbitMQ queue in the same application, publishing a message to each, and verifying both handlers receive their respective messages.

**Acceptance Scenarios**:

1. **Given** a subscription configured for a Kafka destination and another for a RabbitMQ destination, **When** messages arrive on both, **Then** both handlers are invoked independently.
2. **Given** a destination that exists on only one transport, **When** subscribing, **Then** the subscription is created on the correct transport as determined by the same routing rules used for publishing.
3. **Given** an active subscription on one transport, **When** the other transport experiences a failure, **Then** the healthy subscription continues processing without interruption.

---

### User Story 5 - Lifecycle and Health per Transport (Priority: P3)

As a developer, I want lifecycle events and health checks to report per-transport status so that I can monitor each broker independently and react to partial outages.

**Why this priority**: Observability is important but builds on top of the core multi-transport functionality. Developers can use the system without this, but production readiness requires it.

**Independent Test**: Can be fully tested by starting both transports, then simulating a disconnection on one, and verifying lifecycle events and health checks accurately reflect the per-transport state.

**Acceptance Scenarios**:

1. **Given** both transports are connected, **When** health check is queried, **Then** the result includes individual status for each registered transport.
2. **Given** one transport disconnects, **When** a lifecycle event is emitted, **Then** the event identifies which transport is affected.
3. **Given** one transport is unhealthy and the other is healthy, **When** the aggregate health is queried, **Then** the overall status reflects degraded (not fully down).

---

### Edge Cases

- What happens when a message is published to a destination with no matching routing rule and no default transport? A clear, descriptive error must be raised.
- What happens when a transport is registered but fails to connect at startup? The bus should still start with the healthy transports and report the failed one as unhealthy. Publish or subscribe operations targeting that transport MUST be rejected immediately with a descriptive error indicating the transport is unavailable.
- What happens when all transports are unhealthy? The aggregate health status is reported as unhealthy and publish/subscribe operations fail with descriptive errors.
- What happens when a routing rule references a transport name that was not registered? Configuration validation must catch this at startup and raise an error immediately (fail-fast).
- What happens when two routing rules conflict (e.g., same destination pattern mapped to different transports)? The system MUST raise an error at configuration/startup time (fail-fast) to prevent silent misrouting.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST support registering two or more transports within a single message bus instance.
- **FR-002**: The system MUST allow routing rules that map destination name patterns to specific transports.
- **FR-003**: The system MUST allow routing rules that map message types (CLR types) to specific transports.
- **FR-004**: When both a destination-based rule and a type-based rule match, the destination-based rule MUST take precedence.
- **FR-005**: The system MUST support designating one transport as the default, used when no routing rule matches.
- **FR-006**: The system MUST raise a clear error when a message cannot be routed (no matching rule and no default transport).
- **FR-007**: The system MUST resolve the target transport for subscribe operations using the same routing rules as publish operations (destination-based resolution).
- **FR-008**: The system MUST allow subscribing to destinations on multiple transports simultaneously within the same application.
- **FR-009**: A failure on one transport MUST NOT impact subscriptions or publish operations on other healthy transports.
- **FR-010**: Health checks MUST report individual status for each registered transport.
- **FR-011**: Lifecycle events MUST identify which transport they relate to.
- **FR-012**: The system MUST validate transport routing configuration at startup and fail fast if a routing rule references an unregistered transport.
- **FR-013**: The system MUST remain backward compatible — applications using a single transport with existing configuration MUST work without changes.
- **FR-014**: Routing rules MUST support wildcard/glob patterns for destination matching (e.g., "orders.*").

### Key Entities

- **Transport Registration**: A named transport instance (e.g., "rabbitmq", "kafka") with its connection configuration, representing one active broker connection.
- **Routing Rule**: A rule that maps either a destination pattern or a message type to a named transport, used by the bus to decide where to send a message.
- **Transport Router**: The decision-making component that evaluates routing rules and selects the appropriate transport for a given publish or subscribe operation.
- **Aggregate Health**: A composite health result that combines individual transport health statuses into an overall system health assessment.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Developers can configure and use two or more transports in a single application with no more setup effort than configuring each transport individually.
- **SC-002**: Publishing a message to a routed destination selects the correct transport 100% of the time based on configured rules.
- **SC-003**: A failure on one transport does not cause message loss or processing delays on other healthy transports.
- **SC-004**: Applications using a single transport continue to work with zero configuration changes after upgrading.
- **SC-005**: Health checks return per-transport status within the same response time as current single-transport health checks.
- **SC-006**: Misconfigured routing (e.g., referencing an unregistered transport) is detected and reported at application startup, not at runtime.

## Assumptions

- Transport names are unique identifiers provided during registration (e.g., "rabbitmq", "kafka"). If not explicitly named, the transport's built-in `Name` property from `IMessageBusTransport` is used.
- Destination patterns use a simple glob/wildcard syntax (e.g., "orders.*" matches "orders.created", "orders.updated"). The exact pattern syntax will be determined during planning.
- When a message type matches via class hierarchy, the most specific (closest ancestor) rule wins. Only the class inheritance chain is walked (up to but not including `object`); interfaces are not considered for routing.
- Reconnection behavior configured via `WithReconnection` applies independently to each transport.
- The existing `MessagePublishOptions` is sufficient for routing decisions — no new options are needed for the publisher to explicitly select a transport. Routing is always configuration-driven, not per-call; there is no per-publish transport override.
