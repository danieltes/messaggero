# Adapter Delivery Semantics

This document describes the delivery guarantees and behaviour of each transport adapter included with Messaggero.

---

## Kafka (`Messaggero.Kafka`)

| Property | Value |
|----------|-------|
| **Delivery guarantee** | At-least-once |
| **Ordering** | Per-partition ordering (key = message type) |
| **Ack model** | Manual offset commit via `StoreOffset` + `Commit` |
| **Nack behaviour** | No-op — message replayed on consumer restart |
| **Auto-commit** | Disabled (`EnableAutoCommit = false`) |
| **Publisher confirms** | Enabled via `ProduceAsync` (awaits broker ack) |
| **Dead-letter** | Managed by application retry policy; no native DLQ |
| **Prefetch** | Controlled by consumer `MaxPollIntervalMs` and `PrefetchCount` option |

### Key Facts

- Messages are produced to a topic named after the destination (typically the message type lowercased).
- The `acks=all` default ensures the broker writes to all in-sync replicas before acknowledging.
- Consumer group assignment is automatic; rebalances may cause duplicate delivery.
- `AcknowledgeAsync` stores the offset and commits; uncommitted messages are redelivered on restart.
- `RejectAsync` is a no-op — the offset is not committed, so the message will be redelivered when the consumer restarts.

---

## RabbitMQ (`Messaggero.RabbitMQ`)

| Property | Value |
|----------|-------|
| **Delivery guarantee** | At-least-once |
| **Ordering** | Per-queue FIFO |
| **Ack model** | Manual `BasicAckAsync` per delivery tag |
| **Nack behaviour** | `BasicNackAsync` with `requeue: false` — message discarded or routed to DLX if configured |
| **Publisher confirms** | Enabled via `CreateChannelOptions(publisherConfirmationsEnabled: true)` |
| **Dead-letter** | Configured via broker-side DLX policy; nacked messages route to DLX |
| **Prefetch** | Controlled by `BasicQosAsync(prefetchCount)` |

### Key Facts

- Each destination maps to a queue (declared on subscribe).
- Publisher confirms are enabled at channel creation using `CreateChannelOptions`. `BasicPublishAsync` waits for the broker confirmation before returning.
- `AcknowledgeAsync` calls `BasicAckAsync` with the delivery tag stored in message headers.
- `RejectAsync` calls `BasicNackAsync` with `requeue: false`, causing the message to be discarded or routed to a dead-letter exchange if one is bound.
- Automatic connection recovery is enabled by default (`AutomaticRecoveryEnabled = true`).

---

## InMemory (`Messaggero.Testing`)

| Property | Value |
|----------|-------|
| **Delivery guarantee** | At-most-once (no persistence) |
| **Ordering** | FIFO per destination |
| **Ack model** | Remove from pending set |
| **Nack behaviour** | Move to internal dead-letter list |
| **Publisher confirms** | Not applicable (synchronous in-process) |
| **Dead-letter** | Internal `DeadLetterMessages` list accessible for assertions |
| **Prefetch** | Not applicable |

### Key Facts

- Intended **only for unit and integration testing** — not for production use.
- Messages are stored in `ConcurrentQueue<Message>` per destination.
- Subscribers receive messages immediately upon publish if subscribed.
- `AcknowledgeAsync` removes the message from the pending set.
- `RejectAsync` moves the message to an internal dead-letter list.
- `PublishAsync` throws `InvalidOperationException` if the adapter has not been started.
- No network I/O — all operations are in-memory and synchronous.

---

## Choosing an Adapter

| Scenario | Recommended |
|----------|-------------|
| High-throughput event streaming | Kafka |
| Task queues with complex routing | RabbitMQ |
| Unit/integration tests | InMemory |
| Fan-out across brokers | Kafka + RabbitMQ (multi-transport routing) |
