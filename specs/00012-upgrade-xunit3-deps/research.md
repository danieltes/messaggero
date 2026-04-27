# Research: Upgrade xUnit 3 and Dependencies

**Feature**: 00012-upgrade-xunit3-deps  
**Date**: 2026-04-26  
**Status**: Complete — all NEEDS CLARIFICATION items resolved

---

## 1. xUnit v3 Package Naming and Migration Path

**Decision**: Replace `xunit` (2.9.3) with `xunit.v3` (3.2.2). Keep `xunit.runner.visualstudio` at the same package ID but upgrade from 3.0.2 to 3.1.5.

**Rationale**: xUnit v3 deliberately changed the package name from `xunit` to `xunit.v3` to force a conscious upgrade decision and to allow proper SemVer versioning going forward. The `xunit` package on NuGet stays at 2.x forever; 3.x is only available under `xunit.v3`. Source: [xUnit v3 migration guide](https://xunit.net/docs/getting-started/v3/migration).

For the Visual Studio runner, the official migration guide says: *"xunit.runner.visualstudio: Make sure to pick up a 3.x.y version"* — meaning the package ID is unchanged but version 3.x is required. Current 3.0.2 is already on the correct major line; upgrading to 3.1.5 (latest stable) is sufficient.

**Alternatives considered**:  
- `xunit.v3.runner.visualstudio` (3.2.2) exists as a separate package. Research shows it is an internal infrastructure package used by the xUnit team's own tooling, not the package users should reference. The user-facing runner remains `xunit.runner.visualstudio` at 3.x.

---

## 2. Test Projects Must Become Executable Projects

**Decision**: Add `<OutputType>Exe</OutputType>` to all three test project files (Unit, Contract, Integration).

**Rationale**: In xUnit v3, test assemblies are stand-alone executables. The official migration guide requires changing `OutputType` from `Library` (or absent) to `Exe`. Without this change, `dotnet build` will succeed but `dotnet test` will fail at runtime with a diagnostic about the project not being executable. All three test projects currently omit `OutputType` (defaulting to `Library`).

**Alternatives considered**: None — this is a mandatory xUnit v3 architectural requirement.

---

## 3. IAsyncLifetime — No Action Required

**Decision**: No code changes needed for `IAsyncLifetime`.

**Rationale**: A full scan of all test source files (`tests/**/*.cs`) found **zero usages** of `IAsyncLifetime`. The xUnit v3 breaking change (interface now inherits `IAsyncDisposable`; `DisposeAsync` signature changes from `Task` to `ValueTask`) does not apply to this codebase.

---

## 4. Assert.ThrowsAsync — Compatible, No Change Required

**Decision**: The single `Assert.ThrowsAsync<T>` usage in `MessageBusTests.cs` is compatible with xUnit v3 and requires no modification.

**Rationale**: xUnit v3 retains `Assert.ThrowsAsync<T>`. The usage `await Assert.ThrowsAsync<NoRouteFoundException>(act)` is a straightforward pattern that does not rely on AggregateException wrapping or any xUnit 2.x-specific behaviour. No regressions are expected.

---

## 5. CollectionBehavior / Parallelism — No Action Required

**Decision**: No parallelism configuration changes needed.

**Rationale**: A full scan of all test source files found **zero usages** of `[assembly: CollectionBehavior]`, `DisableTestParallelization`, or any xUnit 2.x parallelism attributes. xUnit v3 changes parallelism defaults, but since the test projects carry no explicit parallelism configuration, there is nothing to audit or migrate.

---

## 6. xunit.abstractions — Not Present, No Action Required

**Decision**: No `xunit.abstractions` package reference exists in any project file.

**Rationale**: Searching all `.csproj` files confirms no project references `xunit.abstractions`. The package was never added to this solution. No code-level changes are needed for this breaking change.

---

## 7. Microsoft.Extensions.* Target Version

**Decision**: Upgrade all `Microsoft.Extensions.*` packages to `10.0.7` (not `10.0.0` as stated in the original issue).

**Rationale**: At the time of migration (2026-04-26), the latest stable release of the `Microsoft.Extensions.*` packages is `10.0.7`. Per the spec's definition of "latest stable" (highest version without a pre-release suffix), 10.0.7 is the correct target. The spec's FR-005 mentions `10.0.0` as the drop-preview goal; SC-005 (every package at latest stable) supersedes this and 10.0.7 satisfies both.

**Affected packages and project files**:
| Package | Project(s) |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `Messaggero.csproj` |
| `Microsoft.Extensions.Logging.Abstractions` | `Messaggero.csproj` |
| `Microsoft.Extensions.Options` | `Messaggero.csproj` |
| `Microsoft.Extensions.Hosting.Abstractions` | `Messaggero.csproj` |
| `Microsoft.Extensions.DependencyInjection` | `Messaggero.Tests.Unit.csproj`, `Messaggero.Tests.Integration.csproj` |

---

## 8. Directory.Build.props — No Package Version Changes Required

**Decision**: `Directory.Build.props` requires no modifications for this migration.

**Rationale**: Inspection of `Directory.Build.props` confirms it contains only packaging metadata (Version, Authors, Description, etc.) and no `<PackageVersion>` or `<PackageReference>` entries that centralise dependency versions. Per FR-013 and the clarification decision (partial only — update what's already there), there is nothing to update. All package versions live exclusively in the individual `.csproj` files.

---

## 9. NSubstitute — Already at Latest Stable

**Decision**: No change to `NSubstitute`. Already at the latest stable release.

**Rationale**: NuGet API confirms `NSubstitute` latest stable is `5.3.0`, which is exactly the version currently referenced in `Messaggero.Tests.Unit.csproj`. No upgrade is needed; FR-008 is satisfied by the current version.

---

## 10. Full Version Resolution Table

| Package | Current | Target | Notes |
|---|---|---|---|
| `xunit` | `2.9.3` | **Remove** | Replaced by `xunit.v3` |
| `xunit.v3` | *(absent)* | `3.2.2` | New package ID for xUnit 3 |
| `xunit.runner.visualstudio` | `3.0.2` | `3.1.5` | Same package, version bump |
| `Microsoft.NET.Test.Sdk` | `17.14.0` | `18.5.0` | All three test projects |
| `Assertivo` | `0.1.2` | `0.2.0` | All three test projects |
| `NSubstitute` | `5.3.0` | `5.3.0` | Already latest; no change |
| `Microsoft.Extensions.DependencyInjection` | `10.0.0-preview.3.25171.5` | `10.0.7` | Unit + Integration test projects |
| `Testcontainers.Kafka` | `4.4.0` | `4.11.0` | Integration tests |
| `Testcontainers.RabbitMq` | `4.4.0` | `4.11.0` | Integration tests |
| `BenchmarkDotNet` | `0.14.0` | `0.15.8` | Benchmarks project |
| `Confluent.Kafka` | `2.8.0` | `2.14.0` | `Messaggero.Kafka.csproj` |
| `RabbitMQ.Client` | `7.1.2` | `7.2.1` | `Messaggero.RabbitMQ.csproj` |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | `Messaggero.csproj` |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | `Messaggero.csproj` |
| `Microsoft.Extensions.Options` | `10.0.0-preview.3.25171.5` | `10.0.7` | `Messaggero.csproj` |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.0-preview.3.25171.5` | `10.0.7` | `Messaggero.csproj` |

---

## 11. Code Change Impact Summary

| Change Type | Count | Details |
|---|---|---|
| `.csproj` package version updates | 8 files | All test and source project files |
| `<OutputType>Exe</OutputType>` additions | 3 files | Unit, Contract, Integration test projects |
| Source code (`*.cs`) changes | 0 | No code-level breaking changes found in this codebase |
| `Directory.Build.props` changes | 0 | No package versions centralised there |

The migration is **package-version-only plus OutputType additions** — no C# source changes are required.
