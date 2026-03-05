# Data Model: Multi-Transport Routing

**Feature**: 002-multi-transport-routing  
**Date**: 2026-03-04

## Entities

### RoutingRule

Represents a single rule that maps either a destination pattern or a message type to a named transport.

| Field | Type | Description |
|-------|------|-------------|
| TransportName | string | Name of the target transport (must match a registered transport) |
| DestinationPattern | DestinationPattern? | Compiled glob pattern for destination matching (null for type-based rules) |
| MessageType | Type? | CLR type for type-based matching (null for destination-based rules) |

**Constraints**:
- Exactly one of `DestinationPattern` or `MessageType` must be non-null.
- `TransportName` must reference a transport registered in the builder.
- Two destination rules with identical patterns mapping to different transports are invalid (fail-fast at startup).
- Two type rules with identical types mapping to different transports are invalid (fail-fast at startup).

**Relationships**:
- Owned by `TransportRouter` (1:N — a router holds many rules)
- References a registered `IMessageBusTransport` by name

---

### DestinationPattern

A compiled glob pattern for matching destination strings. Created once at configuration time; matching is allocation-free.

| Field | Type | Description |
|-------|------|-------------|
| RawPattern | string | Original pattern string (e.g., "orders.*") |
| Matcher | Func\<string, bool\> | Compiled match delegate |
| Specificity | PatternSpecificity | Enum: Exact, SingleWildcard, MultiWildcard |

**Pattern syntax**:
- `.` = segment separator
- `*` = match exactly one segment
- `**` = match one or more segments
- No wildcards = exact match

**Specificity ordering** (most specific first):
1. `Exact` — no wildcards (e.g., "orders.created")
2. `SingleWildcard` — contains `*` but not `**` (e.g., "orders.*")
3. `MultiWildcard` — contains `**` (e.g., "orders.**")

---

### TransportRouter (implements ITransportRouter)

The decision-making component that evaluates routing rules and selects the appropriate transport.

| Field | Type | Description |
|-------|------|-------------|
| Transports | IReadOnlyDictionary\<string, IMessageBusTransport\> | All registered transports keyed by name |
| DestinationRules | IReadOnlyList\<RoutingRule\> | Destination-based rules, sorted by specificity (most specific first) |
| TypeRules | IReadOnlyList\<RoutingRule\> | Type-based rules |
| DefaultTransportName | string? | Name of the default transport (fallback) |
| TypeCache | ConcurrentDictionary\<Type, string?\> | Cached type→transport resolutions |

**Behavior — ResolveTransport(destination, messageType?)**:
1. Iterate `DestinationRules` (sorted by specificity). Return first matching transport.
2. If `messageType` is provided and no destination match: walk class hierarchy from exact type to base classes (excluding `object`). Return first matching type rule's transport.
3. If no match: return default transport.
4. If no default: throw `InvalidOperationException` with descriptive message.

**Validation (at startup)**:
- All rule `TransportName` values must exist in `Transports` dictionary.
- No two destination rules with same pattern pointing to different transports.
- No two type rules with same type pointing to different transports.
- Violations throw `InvalidOperationException`.

---

### TransportHealthEntry (new)

A per-transport health status entry within the aggregate health result.

| Field | Type | Description |
|-------|------|-------------|
| TransportName | string | Name of the transport |
| Status | HealthStatus | Healthy, Degraded, or Unhealthy |
| Description | string? | Optional diagnostic message |

**Relationship**:
- Contained in `HealthCheckResult.TransportEntries` (1:N)

---

### HealthCheckResult (extended)

Existing entity extended to support per-transport reporting.

| Field | Type | Description |
|-------|------|-------------|
| Status | HealthStatus | Aggregate status (existing field) |
| Description | string? | Aggregate description (existing field) |
| TransportEntries | IReadOnlyList\<TransportHealthEntry\> | Per-transport health entries (new) |

**Aggregation**:
- All Healthy → Status = Healthy
- Mix → Status = Degraded
- All Unhealthy → Status = Unhealthy

**Backward compatibility**: When only one transport is registered, `TransportEntries` has one element and aggregate status equals that element's status.

---

## State Transitions

### Transport Connection Lifecycle (per transport)

```
Disconnected → Connecting → Connected → Disconnecting → Disconnected
                    ↓                        ↑
                Failed ──(reconnect)──→ Connecting
```

Each transport transitions independently. The message bus is considered "started" when at least one transport reaches `Connected`.

### Routing Resolution (stateless, per operation)

```
Publish/Subscribe call
  → Evaluate destination rules (specificity order)
    → Match found? → Use matched transport
    → No match? → Evaluate type rules (class hierarchy)
      → Match found? → Use matched transport
      → No match? → Use default transport
        → Default exists? → Use it
        → No default? → Throw InvalidOperationException
```

## Invariants

1. **Transport names are unique**: No two transports may share the same name within a bus instance.
2. **Rules reference valid transports**: Every routing rule's `TransportName` must exist in the registered transports dictionary.
3. **No conflicting rules**: Two destination rules with identical patterns or two type rules with identical types must map to the same transport.
4. **Default is optional but must be valid**: If a default transport name is specified, it must reference a registered transport.
5. **Single transport = implicit default**: When exactly one transport is registered with no routing rules, it is the implicit default.
