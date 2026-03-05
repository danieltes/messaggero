# Research: Multi-Transport Routing

**Feature**: 002-multi-transport-routing  
**Date**: 2026-03-04

## R1: Destination Pattern Matching Strategy

**Task**: Determine how destination-based routing rules match destination strings (e.g., "orders.*" matches "orders.created").

### Decision: Custom compiled pattern matcher

**Rationale**: 
- `Microsoft.Extensions.FileSystemGlobbing` uses `/` as separator, allocates on every match, and is designed for filesystem enumeration — unsuitable for hot-path string matching.
- Regex-based matching works but introduces allocation overhead per match from `Match` objects.
- A custom compiled matcher converts glob patterns to delegates at startup (one-time cost) and evaluates with zero allocations on the hot path.

**Pattern syntax**:
- `.` is the segment separator
- `*` matches exactly one segment: `orders.*` matches `orders.created` but not `orders.created.v2`
- `**` matches one or more segments: `orders.**` matches `orders.created` and `orders.created.v2`  
- No wildcards = exact match: `orders.created` matches only `orders.created`

**Alternatives considered**:
- `FileSystemGlobbing`: Rejected — wrong separator model, allocates on hot path, overkill features.
- `Regex.IsMatch`: Rejected — allocates `Match` objects; compiled regex is faster than interpreted but still slower than direct delegate invocation for simple patterns.
- Simple `string.StartsWith/EndsWith`: Rejected — too limited for mid-pattern wildcards like `orders.*.v2`.

**Performance characteristics**:
- Compilation: O(pattern length), one-time at startup
- Matching: O(segment count), typically 2–4 iterations, zero allocations
- Memory: one delegate per pattern, negligible

---

## R2: Multi-Transport DI Registration Strategy

**Task**: Determine how multiple transports are registered and resolved from DI when only one `IMessageBusTransport` was previously registered.

### Decision: Named transport dictionary in MessageBusBuilder, keyed by transport name

**Rationale**:
- The current `MessageBusBuilder` holds a single `Transport` property. Multiple transports need a collection.
- DI keyed services (new in .NET 8+) could be used, but they add complexity and couple the router to the DI container. A simpler approach: the builder collects transports into a dictionary, and the `MessageBus` receives a `IReadOnlyDictionary<string, IMessageBusTransport>` (or a wrapper).
- Transport names come from `IMessageBusTransport.Name` (already exists on the interface) unless explicitly overridden during registration.

**Design**:
- `MessageBusBuilder` gains: `AddTransport(string name, IMessageBusTransport transport)` and transport extension methods like `UseKafka`/`UseRabbitMq` continue to work but register into the collection.
- For backward compatibility, the existing `UseTransport(transport)` method registers the transport using its `.Name` property and marks it as default.
- When only one transport is registered, it is automatically the default — zero-config backward compatibility.

**Alternatives considered**:
- .NET keyed services (`AddKeyedSingleton`): Rejected — couples routing to DI container, harder to validate at startup, cannot enumerate registered transports easily.
- Multiple `IMessageBus` instances: Rejected — breaks the "single bus" abstraction, forces consumers to know about transport topology.
- Named service pattern via factory: Rejected — over-engineered for a known-at-startup set of transports.

---

## R3: Routing Rule Evaluation Order & Conflict Detection

**Task**: Determine evaluation order when multiple rules could match, and how conflicts are detected.

### Decision: Destination rules first (most-specific-first), then type rules (most-derived-first), then default

**Rationale**:
- Per spec (FR-004): destination-based rules take precedence over type-based rules.
- Within destination rules: exact matches should beat wildcard patterns. Specificity ordering: exact > single-wildcard (`*`) > multi-wildcard (`**`).
- Within type rules: the most derived type in the class hierarchy wins (walk from exact type toward `object`, stop at first match).
- Conflict detection: at startup, if two destination rules have identical patterns mapping to different transports, throw `InvalidOperationException` (fail-fast per clarification).

**Evaluation algorithm**:
1. Check destination-based rules in specificity order (exact → `*` → `**`). Return first match.
2. If no destination match, check type-based rules walking up the class hierarchy from the exact type. Return first match.
3. If no type match, use default transport (if configured).
4. If no default, throw routing error.

**Conflict detection algorithm** (at startup):
- Build a set of (normalized pattern → transport name) pairs. If any pattern appears twice with different transport names, throw.
- Type-based rules: same type mapped to different transports → throw.

**Alternatives considered**:
- Priority-based ordering (user assigns numeric priority): Rejected — adds configuration burden without clear benefit over specificity-based ordering.
- Allow overlapping rules (first-registered wins): Rejected — clarification session decided on fail-fast for conflicts.

---

## R4: MessageBus Refactoring for Multi-Transport

**Task**: Determine how `MessageBus` changes from single `IMessageBusTransport` to multi-transport.

### Decision: MessageBus delegates to ITransportRouter for transport selection, holds all transports

**Rationale**:
- `MessageBus` currently holds a single `_transport` field. It will instead hold an `ITransportRouter` that encapsulates the transport collection and routing logic.
- The `ITransportRouter` interface has a single method: `IMessageBusTransport ResolveTransport(string destination, Type? messageType)`.
- `MessageBus.PublishAsync<T>` calls `_router.ResolveTransport(destination, typeof(T))` to get the transport, then delegates as before.
- `MessageBus.SubscribeAsync<T>` calls `_router.ResolveTransport(destination, messageType: null)` — subscribe uses destination-only resolution per clarification.
- Health check aggregates results from all transports.
- Lifecycle events are forwarded from all transports, each already tagged with `TransportName`.

**Connect/disconnect lifecycle**:
- `MessageBus` connects all transports on first operation (or via explicit startup). Connection failures are caught per-transport; the bus starts with whatever transports connect successfully.
- `DisposeAsync` disconnects all transports.

**Alternatives considered**:
- Composite transport pattern (single `IMessageBusTransport` that wraps multiple): Rejected — the composite would need routing logic, making it tightly coupled and harder to test than a separate router.
- Strategy pattern with factory: Rejected — over-abstracted for a compile-time-known set of transports.

---

## R5: Health Check Aggregation

**Task**: Determine how per-transport health checks aggregate into a single `HealthCheckResult`.

### Decision: Extend HealthCheckResult with per-transport entries and aggregate status

**Rationale**:
- Current `HealthCheckResult` has a single status. For multi-transport, it needs:
  - An aggregate status (Healthy if all healthy, Degraded if some unhealthy, Unhealthy if all unhealthy)
  - A collection of per-transport entries: `IReadOnlyList<TransportHealthEntry>` where each has `TransportName`, `Status`, `Description`.
- Backward compatible: single-transport still returns a `HealthCheckResult` with one entry. The aggregate status equals that one transport's status.

**Aggregation rules**:
- All Healthy → Healthy
- Mix of Healthy + Unhealthy → Degraded  
- All Unhealthy → Unhealthy

**Alternatives considered**:
- Separate health check per transport via DI: Rejected — forces consumers to know transport names, breaks the single-bus abstraction.
- Return dictionary instead of structured type: Rejected — less discoverable, no type safety.

---

## R6: Type-Based Routing Class Hierarchy Walk

**Task**: Determine how type-based routing walks the class hierarchy.

### Decision: Build a cached type→transport lookup using Type.BaseType chain

**Rationale**:
- Per clarification: walk class hierarchy only (no interfaces), stop before `object`.
- At publish time: check exact type first, then `BaseType`, then `BaseType.BaseType`, etc.
- Cache resolved type→transport mappings after first resolution to avoid repeated hierarchy walks.
- Use `ConcurrentDictionary<Type, string?>` for thread-safe caching.

**Alternatives considered**:
- Pre-compute all possible derived types at startup: Rejected — impossible without assembly scanning, and new types could be loaded at runtime.
- Include interfaces: Rejected — clarification session explicitly excluded interfaces to avoid ambiguity.

---

## R7: Backward Compatibility Strategy

**Task**: Ensure applications using a single transport require zero changes.

### Decision: Single-transport registration creates an implicit default with no routing rules needed

**Rationale**:
- When only one transport is registered (via existing `UseTransport`, `UseKafka`, or `UseRabbitMq` without naming), it becomes the default transport automatically.
- No routing rules needed — all destinations route to the single transport.
- The existing `MessageBusBuilder.UseTransport(IMessageBusTransport)` continues to work, registering the transport by its `Name` property and marking it as default.
- The existing constructor `MessageBus(IMessageBusTransport, IMessageSerializer, ILogger?)` is kept as an internal/convenience path but the primary DI path uses the new router.

**Alternatives considered**:
- Require explicit default designation even for single transport: Rejected — breaks backward compatibility, adds unnecessary configuration.
