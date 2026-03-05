# Implementation Plan: Message Bus Abstraction Library

**Branch**: `001-message-bus-abstraction` | **Date**: 2026-03-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-message-bus-abstraction/spec.md`

## Summary

Build a transport-agnostic messaging library (NuGet package) in C# / .NET 10 that
abstracts RabbitMQ and Kafka behind a unified publish/subscribe interface. Developers
configure a transport once; application code stays identical when switching brokers.
The library provides at-least-once delivery, per-key ordering, configurable consumer
concurrency, competing-consumer groups, exponential-backoff reconnection, pluggable
serialization (JSON default), and structured lifecycle events for observability.

## Technical Context

**Language/Version**: C# 13 / .NET 10  
**Primary Dependencies**: Confluent.Kafka (Kafka transport), RabbitMQ.Client (RabbitMQ transport), System.Text.Json (default serializer), Microsoft.Extensions.DependencyInjection.Abstractions (DI integration), Microsoft.Extensions.Logging.Abstractions (structured logging)  
**Storage**: N/A — stateless library; brokers own message persistence  
**Testing**: xUnit, FluentAssertions, NSubstitute (mocking), Testcontainers (integration tests with real brokers), BenchmarkDotNet (performance regression)  
**Target Platform**: .NET 10 (cross-platform: Linux, Windows, macOS)  
**Project Type**: Library (NuGet package)  
**Performance Goals**: ≥ 10,000 msg/s per node; ≤ 50 ms p95 end-to-end latency (publish → handler) with co-located broker  
**Constraints**: ≤ 200 ms p95 message delivery (constitution), ≤ 100 ms p95 API reads, async-only public API surface, zero allocations on hot publish path where feasible  
**Scale/Scope**: Two transport implementations (RabbitMQ, Kafka); designed for extensibility to additional transports without core changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I — Code Quality (NON-NEGOTIABLE)

| Rule | Status | Notes |
|---|---|---|
| Single responsibility per module/type | ✅ PASS | Each entity (Transport, Publisher, Subscriber, Envelope, ErrorStrategy) is a separate concern |
| Automated formatters/linters enforced | ✅ PASS | Plan includes `dotnet format` + analyzer packages in CI |
| Static analysis zero warnings | ✅ PASS | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in Directory.Build.props |
| Cyclomatic complexity ≤ 10 per function | ✅ PASS | Will be enforced via Roslyn analyzers (CA1502) |
| No dead code | ✅ PASS | Standard .NET analyzers catch unused members |
| Public API doc-comments | ✅ PASS | `<GenerateDocumentationFile>true</GenerateDocumentationFile>` + CS1591 warning-as-error |

### Principle II — Testing Standards (NON-NEGOTIABLE)

| Rule | Status | Notes |
|---|---|---|
| Test-first development | ✅ PASS | Task ordering enforces tests before implementation |
| Coverage ≥ 80% (≥ 95% critical paths) | ✅ PASS | Message delivery is a critical path → 95% target; spec targets ≥ 90% overall |
| Test pyramid maintained | ✅ PASS | Unit (NSubstitute mocks) → Integration (Testcontainers) → no E2E needed (library) |
| Deterministic tests | ✅ PASS | Testcontainers for broker tests; no shared state; no flaky timing |
| Contract tests for public API | ✅ PASS | Contract tests for IMessageBusTransport, IPublisher, ISubscriber interfaces |
| Performance regression tests | ✅ PASS | BenchmarkDotNet suites in CI; p95 regression gate |

### Principle III — User Experience Consistency

| Rule | Status | Notes |
|---|---|---|
| Design system adherence | N/A | Library has no UI |
| Interaction patterns | N/A | Library has no UI |
| Accessibility (WCAG 2.1 AA) | N/A | Library has no UI |
| Error communication (plain language) | ✅ PASS | FR-007, FR-009 mandate actionable error messages; exceptions use clear messages |
| Loading/empty states | N/A | Library has no UI |
| Offline resilience | ✅ PASS | FR-015 auto-reconnect + FR-008 graceful shutdown satisfy this for a network library |

### Principle IV — High Performance & Throughput (NON-NEGOTIABLE)

| Rule | Status | Notes |
|---|---|---|
| Latency budgets | ✅ PASS | SC-004 targets ≤ 50 ms p95; well within constitution's 200 ms budget |
| Throughput ≥ 10k msg/s | ✅ PASS | SC-003 explicitly matches this target |
| Resource efficiency (minimize allocations) | ✅ PASS | Plan calls for zero-alloc hot path, `ArrayPool<byte>`, `Span<T>` where feasible |
| Async non-blocking I/O | ✅ PASS | Entire public API is async (`Task`/`ValueTask`); no synchronous blocking |
| Observability (structured logs, metrics, traces) | ✅ PASS | FR-010 lifecycle events + Microsoft.Extensions.Logging integration |
| Benchmark regression in CI | ✅ PASS | BenchmarkDotNet + CI gate on p95/throughput regression |

**Pre-Phase 0 Gate: ✅ PASSED** — All applicable rules satisfied. No violations to justify.

**Post-Phase 1 Re-check: ✅ PASSED** — Design artifacts (data-model.md, contracts/public-api.md, quickstart.md) reviewed against all four principles. No new violations introduced. Notable design alignment:
- `IMessageBusTransport.PublishAsync` uses `ReadOnlyMemory<byte>` for zero-copy transport layer (Principle IV — Resource Efficiency)
- `IMessageSerializer.Serialize` returns `byte[]` (allocation) but is isolated to the pluggable serializer boundary; transport hot path operates on `ReadOnlyMemory<byte>`
- All public interfaces carry XML doc-comment obligations enforced via CS1591 + `<GenerateDocumentationFile>` (Principle I — Documentation)
- `ErrorStrategy` factory methods (`Retry()`, `DeadLetter()`, `Reject()`) provide actionable, self-documenting API (Principle III — Error Communication)

## Project Structure

### Documentation (this feature)

```text
specs/001-message-bus-abstraction/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Messaggero.Abstractions/         # Core interfaces, envelope, config, events
│   ├── IMessageBusTransport.cs
│   ├── IPublisher.cs
│   ├── ISubscriber.cs
│   ├── MessageEnvelope.cs
│   ├── TransportConfiguration.cs
│   ├── ErrorStrategy.cs
│   ├── LifecycleEvent.cs
│   ├── IMessageSerializer.cs
│   └── Messaggero.Abstractions.csproj
├── Messaggero/                       # Core orchestrator, DI, default serializer
│   ├── MessageBus.cs
│   ├── MessageBusBuilder.cs
│   ├── Serialization/
│   │   └── JsonMessageSerializer.cs
│   ├── Concurrency/
│   │   └── KeyPartitionedProcessor.cs
│   ├── Resilience/
│   │   └── ExponentialBackoffReconnector.cs
│   ├── ServiceCollectionExtensions.cs
│   └── Messaggero.csproj
├── Messaggero.Transport.RabbitMQ/    # RabbitMQ transport implementation
│   ├── RabbitMqTransport.cs
│   ├── RabbitMqPublisher.cs
│   ├── RabbitMqSubscriber.cs
│   ├── RabbitMqConfiguration.cs
│   ├── ServiceCollectionExtensions.cs
│   └── Messaggero.Transport.RabbitMQ.csproj
└── Messaggero.Transport.Kafka/       # Kafka transport implementation
    ├── KafkaTransport.cs
    ├── KafkaPublisher.cs
    ├── KafkaSubscriber.cs
    ├── KafkaConfiguration.cs
    ├── ServiceCollectionExtensions.cs
    └── Messaggero.Transport.Kafka.csproj

tests/
├── Messaggero.Tests.Unit/            # Unit tests (majority)
│   ├── MessageBusTests.cs
│   ├── MessageEnvelopeTests.cs
│   ├── JsonMessageSerializerTests.cs
│   ├── KeyPartitionedProcessorTests.cs
│   ├── ExponentialBackoffReconnectorTests.cs
│   └── Messaggero.Tests.Unit.csproj
├── Messaggero.Tests.Contract/        # Interface contract tests
│   ├── TransportContractTests.cs     # Parameterized across transports
│   ├── PublisherContractTests.cs
│   ├── SubscriberContractTests.cs
│   └── Messaggero.Tests.Contract.csproj
├── Messaggero.Tests.Integration/     # Integration tests (Testcontainers)
│   ├── RabbitMqIntegrationTests.cs
│   ├── KafkaIntegrationTests.cs
│   ├── TransportSwitchTests.cs
│   └── Messaggero.Tests.Integration.csproj
└── Messaggero.Tests.Performance/     # BenchmarkDotNet benchmarks
    ├── PublishBenchmark.cs
    ├── ConsumeBenchmark.cs
    └── Messaggero.Tests.Performance.csproj

Messaggero.sln                        # Solution file
Directory.Build.props                  # Shared build settings (TreatWarningsAsErrors, analyzers)
```

**Structure Decision**: Multi-project library layout following standard .NET conventions.
Four NuGet packages allow consumers to depend on only the transport they need:
`Messaggero.Abstractions` (interfaces), `Messaggero` (core + DI),
`Messaggero.Transport.RabbitMQ`, `Messaggero.Transport.Kafka`.
Test projects are separated by test type (unit, contract, integration, performance)
matching the constitution's test pyramid requirement.

## Complexity Tracking

> No constitution violations detected. No justifications required.
