# Implementation Plan: Broker-Agnostic Messaging Library with Multi-Transport Routing

**Branch**: `00001-broker-agnostic-core` | **Date**: 2026-03-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/00001-broker-agnostic-core/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Build a broker-agnostic messaging library (NuGet package) that exposes a single fluent builder API for publishing and consuming messages across multiple simultaneous broker transports (Kafka and RabbitMQ initially). The library provides message-type-based routing, class-based handlers with lifecycle hooks, configurable retry/dead-letter policies, pluggable serialization, and structured observability — all while keeping application code completely decoupled from broker specifics.

## Technical Context

**Language/Version**: C# latest (C# 13) / .NET 10  
**Primary Dependencies**: Confluent.Kafka (Kafka adapter), RabbitMQ.Client (RabbitMQ adapter), Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, System.Text.Json (default serializer)  
**Storage**: N/A  
**Testing**: xUnit, NSubstitute (mocking), FluentAssertions, Testcontainers (integration tests with Kafka/RabbitMQ), BenchmarkDotNet (performance benchmarks)  
**Target Platform**: .NET 10 (cross-platform: Linux, Windows, macOS)  
**Project Type**: Library (NuGet package)  
**Performance Goals**: ≥10,000 msg/s aggregate throughput with two adapters active; routing resolution ≤0.5 ms p99 for ≤1,000 rules; publish overhead ≤1 ms p99 for payloads ≤1 MB  
**Constraints**: Zero application-code changes when switching/adding transports; at-least-once delivery semantics by default; no sensitive data in logs  
**Scale/Scope**: 21 functional requirements, 6 non-functional requirements, 2 initial broker adapters (Kafka, RabbitMQ), 1 in-memory test adapter

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Requirement | Status | Notes |
|---------|------------|--------|-------|
| I. Spec-First Development | Written specification before code | **PASS** | spec.md exists with full problem statement, acceptance criteria, and measurable success metrics |
| I. Spec-First Development | Broker-agnostic contract + broker-specific deviations | **PASS** | Spec defines shared adapter contract; adapter-specific semantics documented per adapter (FR-012) |
| I. Spec-First Development | Error handling, retry, delivery semantics, ordering | **PASS** | FR-008 typed errors, FR-009 retry policy, FR-021 at-least-once default, ordering per-adapter |
| II. Code Quality | Minimal, consistent, stable public API | **PASS** | Fluent builder is single entry point (FR-014); public surface is spec-constrained |
| II. Code Quality | Abstractions preserve broker capabilities without leaking complexity | **PASS** | Adapter contract designed for extensibility; broker-specific config isolated per adapter |
| II. Code Quality | Explicit, typed error model | **PASS** | FR-008 defines 6 typed error types |
| III. Testing | Unit, integration, contract, E2E tests | **PASS** | In-memory test adapter (NFR-005), contract tests across adapters, Testcontainers for integration |
| III. Testing | Reliability paths tested (retry, dead-letter, idempotency, backpressure, failure recovery) | **PASS** | FR-009, FR-010, FR-019, SC-008 explicitly require these |
| III. Testing | Performance benchmarks | **PASS** | BenchmarkDotNet planned; SC-004, SC-005, SC-006 define targets |
| IV. Developer Experience | Consistent interface across broker implementations | **PASS** | Single fluent builder API; broker-agnostic publish/consume (NFR-001) |
| IV. Developer Experience | Fast getting started with examples | **PASS** | quickstart.md planned; SC-009 targets 20-min onboarding |
| V. Performance | Baseline benchmarks defined | **PASS** | NFR-002 (≤0.5ms routing), NFR-003 (≤1ms publish), NFR-004 (≥10k msg/s) |
| V. Performance | Batching, concurrency, backpressure | **PASS** | FR-019 prefetch/buffer, FR-020 handler concurrency |
| V. Performance | Observability for diagnosis | **PASS** | FR-011 structured logs/metrics; FR-013 no sensitive data logged |
| VI. Compatibility | Shared contracts with documented semantic differences | **PASS** | FR-012 per-adapter documented semantics |
| VI. Compatibility | Explicit delivery guarantees | **PASS** | FR-021 at-least-once default; per-adapter documentation |
| VII. Security | Secure defaults, safe credential handling | **PASS** | TLS/auth delegated to adapter config; FR-013 no secrets logged |
| VII. Security | Safe degradation over silent data loss | **PASS** | NFR-006 adapter isolation; FR-008 typed failures; no silent drops |

**Gate Result**: **PASS** — All constitution articles satisfied. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Messaggero/                          # Core library (NuGet: Messaggero)
│   ├── Abstractions/                    # Public interfaces and contracts
│   │   ├── IMessageBus.cs              # publish/subscribe surface
│   │   ├── IMessageHandler.cs          # handler contract + lifecycle hooks
│   │   ├── ITransportAdapter.cs        # adapter contract
│   │   └── IMessageSerializer.cs      # serializer/deserializer interface
│   ├── Configuration/                   # Fluent builder and options
│   │   ├── MessagingBuilder.cs
│   │   ├── TransportOptions.cs
│   │   ├── RetryPolicyOptions.cs
│   │   └── RoutingRuleOptions.cs
│   ├── Routing/                         # Routing table and resolution
│   │   ├── RoutingTable.cs
│   │   └── RoutingRule.cs
│   ├── Hosting/                         # MessagingHost runtime
│   │   └── MessagingHost.cs
│   ├── Errors/                          # Typed error model
│   │   ├── PublishFailure.cs
│   │   ├── NoRouteFoundException.cs
│   │   ├── TransportNotFoundException.cs
│   │   ├── TransportDegradedException.cs
│   │   ├── DeserializationException.cs
│   │   └── RetryExhaustedException.cs
│   ├── Model/                           # Message, PublishResult, Destination
│   │   ├── Message.cs
│   │   ├── PublishResult.cs
│   │   └── Destination.cs
│   └── Serialization/                   # Default JSON serializer
│       └── JsonMessageSerializer.cs
│
├── Messaggero.Kafka/                    # Kafka adapter (NuGet: Messaggero.Kafka)
│   ├── KafkaTransportAdapter.cs
│   └── KafkaOptions.cs
│
├── Messaggero.RabbitMQ/                 # RabbitMQ adapter (NuGet: Messaggero.RabbitMQ)
│   ├── RabbitMqTransportAdapter.cs
│   └── RabbitMqOptions.cs
│
└── Messaggero.Testing/                  # In-memory test adapter (NuGet: Messaggero.Testing)
    ├── InMemoryTransportAdapter.cs
    └── TestMessageBus.cs

tests/
├── Messaggero.Tests.Unit/              # Unit tests for core logic
├── Messaggero.Tests.Contract/          # Contract tests across adapters
├── Messaggero.Tests.Integration/       # Integration tests (Testcontainers)
└── Messaggero.Tests.Benchmarks/        # BenchmarkDotNet performance tests
```

**Structure Decision**: Multi-project solution following NuGet library best practices. Core abstractions in `Messaggero`, broker adapters in separate packages (`Messaggero.Kafka`, `Messaggero.RabbitMQ`) so consumers only pull the adapters they need. `Messaggero.Testing` provides an in-memory adapter for unit/contract testing without broker infrastructure.

## Complexity Tracking

> No constitution violations requiring justification.
