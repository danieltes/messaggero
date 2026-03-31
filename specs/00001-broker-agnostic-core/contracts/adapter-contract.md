# Adapter Contract: ITransportAdapter

**Package**: `Messaggero`  
**Date**: 2026-03-29

This document defines the behavioral contract that all transport adapter implementations must satisfy. Contract tests in `Messaggero.Tests.Contract` validate these behaviors uniformly across InMemory, Kafka, and RabbitMQ adapters.

---

## Lifecycle

| Method | Pre-condition | Post-condition | Error behavior |
|--------|--------------|----------------|----------------|
| `StartAsync` | Adapter not started | Connections established; ready to publish/subscribe | Throws on connection failure; adapter remains stopped |
| `StopAsync` | Adapter started | All consumers drained; connections closed; no in-flight messages lost | Best-effort drain; logs warnings for unfinished work |
| `DisposeAsync` | Any state | All resources released | Must not throw |

**Ordering**: `StartAsync` → (publish/subscribe operations) → `StopAsync` → `DisposeAsync`.

---

## Publishing

| Behavior | Requirement |
|----------|-------------|
| Successful publish | Returns `TransportOutcome { Success = true }` with broker-specific metadata |
| Broker unreachable | Returns `TransportOutcome { Success = false, Error = PublishFailure(...) }` |
| Serialized payload | Adapter receives pre-serialized `Message.Payload` (bytes); adapter does NOT re-serialize |
| Destination resolution | Adapter interprets `Destination.Name` per its broker model (topic, exchange, etc.) |
| Thread safety | `PublishAsync` MUST be safe for concurrent callers |

---

## Subscribing

| Behavior | Requirement |
|----------|-------------|
| Message delivery | Invoke `onMessage` callback with deserialized `Message` including populated `SourceTransport` |
| Prefetch/buffering | Respect configured prefetch limit; pause broker fetch when buffer is full (FR-019) |
| Acknowledgement | Do NOT auto-ack; wait for explicit `AcknowledgeAsync` or `RejectAsync` from the host |
| Connection loss | Emit `TransportDegradedException` event; attempt reconnection; do not crash other adapters |

---

## Acknowledgement

| Method | Broker behavior |
|--------|----------------|
| `AcknowledgeAsync` | Kafka: commit offset. RabbitMQ: `BasicAck`. InMemory: remove from pending. |
| `RejectAsync` | Kafka: do not commit (message replayed on restart) + optionally publish to DLT. RabbitMQ: `BasicNack(requeue: false)` → DLX routes. InMemory: move to dead-letter list. |

---

## Delivery Semantics Per Adapter

| Adapter | Default Delivery | Ordering | Notes |
|---------|-----------------|----------|-------|
| Kafka | At-least-once (manual commit) | Per-partition FIFO | Consumer group rebalancing may cause redelivery |
| RabbitMQ | At-least-once (manual ack) | Per-queue FIFO | Prefetch + manual ack; DLX for dead-letter |
| InMemory | At-least-once (simulated) | FIFO (single queue) | For testing only; no persistence |

---

## Contract Test Matrix

Each behavior above MUST be verified by a parameterized contract test that runs against all registered adapter implementations:

1. Publish succeeds → outcome is success with metadata
2. Publish to unavailable broker → outcome is failure with `PublishFailure`
3. Subscribe delivers message with correct payload and source transport
4. Prefetch limit pauses consumption when buffer is full
5. `AcknowledgeAsync` prevents redelivery
6. `RejectAsync` triggers dead-letter routing
7. `StopAsync` drains in-flight messages
8. Adapter failure does not affect other adapter instances
