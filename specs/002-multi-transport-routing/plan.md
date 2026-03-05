# Implementation Plan: Multi-Transport Routing

**Branch**: `002-multi-transport-routing` | **Date**: 2026-03-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-multi-transport-routing/spec.md`

## Summary

Enable the Messaggero message bus to register and use multiple transports simultaneously (e.g., RabbitMQ + Kafka). A transport router selects the correct transport for each publish/subscribe operation based on destination-pattern and message-type routing rules, with a configurable default fallback. Health checks and lifecycle events report per-transport status. The existing single-transport API remains backward compatible.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0 (preview)
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Confluent.Kafka, RabbitMQ.Client
**Storage**: N/A
**Testing**: xUnit, FluentAssertions, NSubstitute, Testcontainers
**Target Platform**: .NET 10.0 (cross-platform library)
**Project Type**: Library (NuGet package)
**Performance Goals**: ≥10,000 msg/s per node; message delivery ≤200ms p95 (constitution); routing lookup must be O(n) in rule count or better, with no per-message allocations on the hot path
**Constraints**: Routing resolution must introduce <1ms overhead; transport failure isolation (one transport down must not block others)
**Scale/Scope**: 2–5 registered transports typical; tens of routing rules

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-Design | Post-Design | Notes |
|-----------|-----------|-------------|-------|
| I. Code Quality | ✅ PASS | ✅ PASS | New types have single responsibilities: `DestinationPattern` (matching), `RoutingRule` (rule data), `TransportRouter` (resolution). All public APIs documented. Internal types are `internal`. |
| II. Testing Standards | ✅ PASS | ✅ PASS | Unit tests for router/pattern, contract tests for builder API, integration tests for multi-transport. |
| III. UX Consistency | ✅ N/A | ✅ N/A | Library — no UI surfaces. |
| IV. High Performance | ✅ PASS | ✅ PASS | Pattern compiled at startup. Matching is zero-alloc O(segments). Type resolution cached. <1ms routing overhead. |

No violations. No complexity tracking entries needed.

## Project Structure

### Documentation (this feature)

```text
specs/002-multi-transport-routing/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── public-api.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── Messaggero.Abstractions/
│   ├── IMessageBus.cs              # Unchanged (backward compatible)
│   ├── IMessageBusTransport.cs     # Unchanged
│   ├── HealthCheckResult.cs        # Extended: per-transport entries
│   └── LifecycleEvent.cs           # Already has TransportName field
├── Messaggero/
│   ├── MessageBus.cs               # Modified: multi-transport delegation
│   ├── MessageBusBuilder.cs        # Extended: multi-transport + routing config
│   ├── ServiceCollectionExtensions.cs  # Modified: register multiple transports
│   └── Routing/                    # NEW directory
│       ├── ITransportRouter.cs     # Router interface
│       ├── TransportRouter.cs      # Routing rule evaluation
│       ├── RoutingRule.cs          # Rule definition (destination/type)
│       └── DestinationPattern.cs   # Glob/wildcard pattern matching
├── Messaggero.Transport.Kafka/
│   └── ServiceCollectionExtensions.cs  # Modified: named transport registration
└── Messaggero.Transport.RabbitMQ/
    └── ServiceCollectionExtensions.cs  # Modified: named transport registration

tests/
├── Messaggero.Tests.Unit/
│   ├── TransportRouterTests.cs       # NEW: routing logic unit tests
│   ├── DestinationPatternTests.cs    # NEW: pattern matching tests
│   └── MessageBusMultiTransportTests.cs  # NEW: multi-transport unit tests
├── Messaggero.Tests.Contract/
│   └── TransportContractTests.cs     # Extended: multi-transport contracts
└── Messaggero.Tests.Integration/
    └── MultiTransportIntegrationTests.cs  # NEW: RabbitMQ+Kafka together
```

**Structure Decision**: No new projects. The routing logic lives in the existing `Messaggero` project under a new `Routing/` folder. This keeps the dependency graph identical and avoids adding complexity.
