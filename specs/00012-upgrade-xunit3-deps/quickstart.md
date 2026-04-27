# Quickstart: Upgrade xUnit 3 and Dependencies

**Feature**: 00012-upgrade-xunit3-deps  
**Branch**: `00012-upgrade-xunit3-deps`

---

## What This Does

Upgrades all NuGet dependencies in the Messaggero solution to their latest stable releases, with a focus on migrating the three test projects from xUnit 2.9.3 to xUnit 3 (`xunit.v3` 3.2.2).

---

## Prerequisites

- .NET 10 SDK installed (`dotnet --version` should show `10.x`)
- Docker available (required for Integration tests via Testcontainers)
- Git on branch `00012-upgrade-xunit3-deps`

---

## Implementation Steps

### Step 1 — Update Source Project Dependencies

**`src/Messaggero/Messaggero.csproj`**: Change all four `Microsoft.Extensions.*` versions from `10.0.0-preview.3.25171.5` to `10.0.7`.

**`src/Messaggero.Kafka/Messaggero.Kafka.csproj`**: Change `Confluent.Kafka` from `2.8.0` to `2.14.0`.

**`src/Messaggero.RabbitMQ/Messaggero.RabbitMQ.csproj`**: Change `RabbitMQ.Client` from `7.1.2` to `7.2.1`.

### Step 2 — Update Benchmarks Project

**`tests/Messaggero.Tests.Benchmarks/Messaggero.Tests.Benchmarks.csproj`**: Change `BenchmarkDotNet` from `0.14.0` to `0.15.8`.

### Step 3 — Migrate Test Projects to xUnit v3

For **each** of the three test projects (Unit, Contract, Integration):

1. **Add `<OutputType>Exe</OutputType>`** inside the `<PropertyGroup>`:
   ```xml
   <OutputType>Exe</OutputType>
   ```

2. **Remove** the `xunit` 2.9.3 package reference:
   ```xml
   <!-- REMOVE THIS -->
   <PackageReference Include="xunit" Version="2.9.3" />
   ```

3. **Add** the `xunit.v3` package reference:
   ```xml
   <PackageReference Include="xunit.v3" Version="3.2.2" />
   ```

4. **Upgrade** `xunit.runner.visualstudio` from `3.0.2` to `3.1.5`.

5. **Upgrade** `Microsoft.NET.Test.Sdk` from `17.14.0` to `18.5.0`.

6. **Upgrade** `Assertivo` from `0.1.2` to `0.2.0`.

**Unit project additional changes**:
- Upgrade `Microsoft.Extensions.DependencyInjection` from `10.0.0-preview.3.25171.5` to `10.0.7`.
- `NSubstitute` stays at `5.3.0` (already latest stable).

**Integration project additional changes**:
- Upgrade `Microsoft.Extensions.DependencyInjection` from `10.0.0-preview.3.25171.5` to `10.0.7`.
- Upgrade `Testcontainers.Kafka` from `4.4.0` to `4.11.0`.
- Upgrade `Testcontainers.RabbitMq` from `4.4.0` to `4.11.0`.

### Step 4 — Restore and Build

```shell
dotnet restore
dotnet build --no-restore
```

Expect: exit code 0, zero warnings.

### Step 5 — Run All Tests

```shell
dotnet test --no-build
```

Expect: all tests pass across Unit, Contract, and Integration projects. Docker must be running for Integration tests (Testcontainers starts Kafka and RabbitMQ containers automatically).

---

## Verification Checklist

- [ ] `dotnet build` exits with code 0 and zero warnings
- [ ] `dotnet test` is green for Unit, Contract, and Integration projects
- [ ] No `xunit` 2.x package reference exists in any `.csproj`
- [ ] No `xunit.abstractions` package reference exists in any `.csproj`
- [ ] No version string with `-preview` suffix remains in any `.csproj`
- [ ] All three test `.csproj` files have `<OutputType>Exe</OutputType>`

---

## Known Compatibility Notes

| Upgrade | Risk | Notes |
|---|---|---|
| `xunit` → `xunit.v3` | Medium | Package rename + OutputType change required; no C# source changes needed for this codebase |
| `Microsoft.Extensions.*` → 10.0.7 | Low | Patch version bumps only; no API changes expected |
| `Confluent.Kafka` 2.8 → 2.14 | Low-Medium | Minor version bump; verify integration tests pass |
| `Testcontainers.*` 4.4 → 4.11 | Low | Patch series bump; API-compatible |
| `BenchmarkDotNet` 0.14 → 0.15 | Low | Minor version; verify benchmarks still compile and run |
| `RabbitMQ.Client` 7.1 → 7.2 | Low | Patch bump; no API changes expected |
| `Assertivo` 0.1 → 0.2 | Low-Medium | Minor version; verify assertion usages still compile |
