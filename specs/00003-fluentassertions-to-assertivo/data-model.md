# Data Model: Replace FluentAssertions with Assertivo

**Feature**: 00003-fluentassertions-to-assertivo
**Date**: 2026-04-18

## Overview

This migration has no data model changes in the traditional sense (no entities, schemas, or state transitions). The "data model" for this feature is the inventory of affected artifacts and the mapping between old and new package/namespace references.

## Affected Artifacts

### Package References (3 files)

| Project File | Old Reference | New Reference |
|---|---|---|
| `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj` | `FluentAssertions` 8.3.0 | `Assertivo` 0.1.2 |
| `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj` | `FluentAssertions` 8.3.0 | `Assertivo` 0.1.2 |
| `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj` | `FluentAssertions` 8.3.0 | `Assertivo` 0.1.2 |

### Using Directives (18 files)

| Test Project | File | Directive Change |
|---|---|---|
| Unit | `Configuration/ScopedHandlerValidationTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Examples/HandlerIsolationExampleTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Hosting/HandlerDispatcherTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Hosting/MessageBusTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Hosting/RetryExecutorTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Observability/LogScrubTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Unit | `Routing/RoutingTableTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `AdapterIsolationTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `FanInDegradedTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `FanInIntegrationTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `FanOutFailureTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `RoutingIntegrationTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Integration | `ScopedSubscriptionTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Contract | `AckNackContractTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Contract | `BackpressureContractTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Contract | `LifecycleContractTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Contract | `PublishContractTests.cs` | `using FluentAssertions;` → `using Assertivo;` |
| Contract | `SubscribeContractTests.cs` | `using FluentAssertions;` → `using Assertivo;` |

### Out of Scope (unchanged)

| Project | Reason |
|---|---|
| `tests/Messaggero.Tests.Benchmarks/` | Does not reference FluentAssertions |
| `src/Messaggero/` | Source library — no assertion references |
| `src/Messaggero.Kafka/` | Source library — no assertion references |
| `src/Messaggero.RabbitMQ/` | Source library — no assertion references |
| `src/Messaggero.Testing/` | Test helper library — no FluentAssertions reference |

## Assertion Pattern Inventory

All patterns map 1:1 from FluentAssertions to Assertivo (see [research.md](research.md) compatibility matrix). No assertion rewrites are expected.

### Patterns by Category

| Category | Methods Used | Count of Usages (approx) |
|---|---|---|
| Object equality | `Be()`, `BeSameAs()`, `BeOfType<T>()` | Medium |
| Null checks | `NotBeNull()`, `BeNull()` | Low |
| Boolean | `BeTrue()`, `BeFalse()` | Low |
| String | `Contain()`, `NotContain()`, `NotBeNullOrEmpty()`, `BeEmpty()` | Medium |
| Numeric | `BeGreaterThanOrEqualTo()`, `BeLessThan()` | Low |
| Collection | `HaveCount()`, `ContainSingle()`, `Contain()`, `BeEmpty()`, `NotBeEmpty()`, `BeEquivalentTo()`, `AllSatisfy()` | High |
| Exception | `Throw<T>()`, `ThrowAsync<T>()` | Medium |
| Drill-down | `.Which.Property.Should().Be()` | Medium |
