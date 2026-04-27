# Data Model: Upgrade xUnit 3 and Dependencies

**Feature**: 00012-upgrade-xunit3-deps  
**Date**: 2026-04-26

---

## Overview

This feature has no persistent data model or domain entities — it is a dependency upgrade. This document instead captures the **package dependency model**: the full set of package references per project, their current state, and their target state after migration.

---

## Package Reference State

### Project: `src/Messaggero/Messaggero.csproj` (Source)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |
| `Microsoft.Extensions.Options` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |

### Project: `src/Messaggero.Kafka/Messaggero.Kafka.csproj` (Source)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `Confluent.Kafka` | `2.8.0` | `2.14.0` | Version bump |

### Project: `src/Messaggero.RabbitMQ/Messaggero.RabbitMQ.csproj` (Source)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `RabbitMQ.Client` | `7.1.2` | `7.2.1` | Version bump |

### Project: `src/Messaggero.Testing/Messaggero.Testing.csproj` (Source)

*No package references. No changes required.*

---

### Project: `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj` (Test)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `xunit` | `2.9.3` | **Remove** | Package removed (replaced) |
| `xunit.v3` *(new)* | *(absent)* | `3.2.2` | Package added (replaces `xunit`) |
| `xunit.runner.visualstudio` | `3.0.2` | `3.1.5` | Version bump |
| `Microsoft.NET.Test.Sdk` | `17.14.0` | `18.5.0` | Version bump |
| `Assertivo` | `0.1.2` | `0.2.0` | Version bump |
| `NSubstitute` | `5.3.0` | `5.3.0` | No change (already latest) |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |

**Project file structural change**: Add `<OutputType>Exe</OutputType>` (required by xUnit v3).

---

### Project: `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj` (Test)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `xunit` | `2.9.3` | **Remove** | Package removed (replaced) |
| `xunit.v3` *(new)* | *(absent)* | `3.2.2` | Package added (replaces `xunit`) |
| `xunit.runner.visualstudio` | `3.0.2` | `3.1.5` | Version bump |
| `Microsoft.NET.Test.Sdk` | `17.14.0` | `18.5.0` | Version bump |
| `Assertivo` | `0.1.2` | `0.2.0` | Version bump |

**Project file structural change**: Add `<OutputType>Exe</OutputType>` (required by xUnit v3).

---

### Project: `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj` (Test)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `xunit` | `2.9.3` | **Remove** | Package removed (replaced) |
| `xunit.v3` *(new)* | *(absent)* | `3.2.2` | Package added (replaces `xunit`) |
| `xunit.runner.visualstudio` | `3.0.2` | `3.1.5` | Version bump |
| `Microsoft.NET.Test.Sdk` | `17.14.0` | `18.5.0` | Version bump |
| `Assertivo` | `0.1.2` | `0.2.0` | Version bump |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0-preview.3.25171.5` | `10.0.7` | Version bump (drop preview) |
| `Testcontainers.Kafka` | `4.4.0` | `4.11.0` | Version bump |
| `Testcontainers.RabbitMq` | `4.4.0` | `4.11.0` | Version bump |

**Project file structural change**: Add `<OutputType>Exe</OutputType>` (required by xUnit v3).

---

### Project: `tests/Messaggero.Tests.Benchmarks/Messaggero.Tests.Benchmarks.csproj` (Benchmarks)

| Package | Current Version | Target Version | Change Type |
|---|---|---|---|
| `BenchmarkDotNet` | `0.14.0` | `0.15.8` | Version bump |

*Note: Already has `<OutputType>Exe</OutputType>`. No xUnit dependency. No structural changes needed.*

---

### Project: `Directory.Build.props`

*No package version properties are centralised in `Directory.Build.props`. No changes required.*

---

## Code-Level Impact Analysis

### C# Source Files Requiring Changes

A scan of all test source files (`tests/**/*.cs`) confirms:

| Pattern | Files Found | Action |
|---|---|---|
| `IAsyncLifetime` usage | 0 | None |
| `xunit.abstractions` import | 0 | None |
| `CollectionBehavior` attribute | 0 | None |
| `DisableTestParallelization` attribute | 0 | None |
| `Assert.ThrowsAsync` usage | 1 (`MessageBusTests.cs:96`) | Compatible with xUnit v3; no change needed |
| `async void` test methods | 0 | None |

**Net code change count: 0 C# source files need modification.**

---

## Change Set Summary

| Category | Files Changed | Changes |
|---|---|---|
| Source `.csproj` package version updates | 3 | Version bumps for Microsoft.Extensions.*, Confluent.Kafka, RabbitMQ.Client |
| Test `.csproj` package changes | 3 | Remove `xunit`, add `xunit.v3`, version bumps for all packages |
| Test `.csproj` structural change | 3 | Add `<OutputType>Exe</OutputType>` |
| Benchmarks `.csproj` | 1 | BenchmarkDotNet version bump |
| C# source files | 0 | None |
| `Directory.Build.props` | 0 | None |
| **Total files modified** | **7** | |
