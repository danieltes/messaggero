# Feature Specification: Upgrade xUnit 3 and Dependencies

**Feature Branch**: `00012-upgrade-xunit3-deps`  
**Created**: 2026-04-26  
**Status**: Draft  
**Input**: User description: "Migrate test projects from xUnit 2.9.3 to xUnit 3 and upgrade all stale dependencies to latest stable releases"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Migrate Test Projects to xUnit 3 (Priority: P1)

A developer maintaining the Messaggero solution upgrades all three test projects (Unit, Contract, Integration) from xUnit 2.9.3 to xUnit 3. They address all breaking changes introduced by xUnit v3 so the existing test suite compiles and runs correctly without any regressions.

**Why this priority**: xUnit 3 is a ground-up rewrite that is actively maintained while xUnit 2.x is in maintenance mode. The existing `xunit.runner.visualstudio` at `3.0.2` already creates a version mismatch with the xUnit 2.9.3 core, making this the most urgent and highest-risk change.

**Independent Test**: Can be fully tested by running `dotnet test` across the three test projects and verifying all tests pass with zero failures or build errors, independent of any other dependency upgrades.

**Acceptance Scenarios**:

1. **Given** all test projects reference xUnit 2.9.3, **When** the package references are updated to xUnit 3.x and breaking changes are resolved, **Then** `dotnet build` completes with exit code 0 and zero warnings.
2. **Given** the solution has been migrated to xUnit 3, **When** `dotnet test` is executed across Unit, Contract, and Integration projects, **Then** all previously passing tests continue to pass with no regressions.
3. **Given** the migration is complete, **When** a search for `xunit.abstractions` package references is performed across all project files, **Then** no references are found.
4. **Given** xUnit 3 changes parallelism defaults, **When** the migration is applied, **Then** any `[assembly: CollectionBehavior]` or `DisableTestParallelization` settings are reviewed and adjusted to preserve the original test isolation intent.

---

### User Story 2 - Upgrade All Other Stale Dependencies (Priority: P2)

A developer upgrades all remaining outdated packages across source and test projects: Microsoft.Extensions.* (drop preview suffix to stable 10.0.0), Confluent.Kafka, RabbitMQ.Client, Testcontainers.*, BenchmarkDotNet, NSubstitute, and Microsoft.NET.Test.Sdk.

**Why this priority**: These upgrades carry lower risk than the xUnit migration because they do not involve framework-level breaking changes to the test runner. However, the Microsoft.Extensions.* preview suffix must be resolved before any release, and outdated packages accumulate security and compatibility risk.

**Independent Test**: Can be tested by running `dotnet build` and `dotnet test` after updating non-xUnit package references, verifying the solution compiles and all tests remain green with all packages at stable versions.

**Acceptance Scenarios**:

1. **Given** Microsoft.Extensions.* packages are on `10.0.0-preview.3.25171.5`, **When** they are updated to `10.0.7` stable across all source and test projects, **Then** all projects compile without errors or warnings related to the version change.
2. **Given** Confluent.Kafka, RabbitMQ.Client, Testcontainers.Kafka, Testcontainers.RabbitMq, BenchmarkDotNet, and NSubstitute are on outdated versions, **When** they are bumped to their latest stable releases, **Then** all integration and benchmark projects build and their tests pass.
3. **Given** `Directory.Build.props` may centralise some version properties, **When** package versions are updated, **Then** `Directory.Build.props` is audited and updated only for versions already declared there; individual project files continue to manage their own package references.

---

### Edge Cases

- What happens when a test uses `xunit.abstractions` types directly? Those references must be migrated to the `Xunit` namespace equivalents present in xUnit 3.
- How does the system handle tests that rely on xUnit 2.x default parallelism behaviour if xUnit 3 changes those defaults? Each such test collection must be explicitly reviewed and annotated to preserve original intent.
- What if a latest stable version of a dependency introduces its own breaking changes? Each upgrade must be individually validated against the test suite.
- How are `IAsyncLifetime` implementations affected when the interface source changes from `xunit.abstractions` to the `Xunit` namespace? All implementations must be updated to import from the correct namespace.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All test projects (Unit, Contract, Integration) MUST reference the `xunit.v3` NuGet package (3.x latest stable) in place of the `xunit` package 2.9.3.
- **FR-002**: The solution MUST build with exit code 0 and zero warnings after all package updates are applied.
- **FR-003**: All tests that passed before migration MUST continue to pass after migration (`dotnet test` green across Unit, Contract, and Integration).
- **FR-004**: No project file in the solution MUST retain a reference to the `xunit.abstractions` package.
- **FR-005**: All `Microsoft.Extensions.*` package references MUST be updated to `10.0.7` stable (removing the preview suffix) in both source and test projects.
- **FR-006**: `xunit.runner.visualstudio` MUST be updated to the latest stable version compatible with xUnit 3.
- **FR-007**: `Microsoft.NET.Test.Sdk` MUST be updated to the latest stable release.
- **FR-008**: `NSubstitute` (Unit tests only) MUST be at its latest stable release. Version 5.3.0 is already the latest stable at the time of this migration — no version change is required.
- **FR-009**: `Testcontainers.Kafka` and `Testcontainers.RabbitMq` (Integration tests) MUST be updated to their latest stable releases.
- **FR-010**: `BenchmarkDotNet` (Benchmarks project) MUST be updated to the latest stable release.
- **FR-011**: `Confluent.Kafka` (Messaggero.Kafka source project) MUST be updated to the latest stable release.
- **FR-012**: `RabbitMQ.Client` (Messaggero.RabbitMQ source project) MUST be updated to the latest stable release.
- **FR-013**: `Directory.Build.props` MUST be audited to confirm whether it centralises any package version properties affected by this migration; if found, they MUST be updated. Research has confirmed that `Directory.Build.props` contains no package version properties at the time of this migration — no changes are required. Full migration of all package versions into `Directory.Build.props` is explicitly OUT OF SCOPE.
- **FR-014**: All xUnit 3 breaking changes in existing test code and project structure MUST be resolved, including: `IAsyncLifetime` namespace changes, `Assert.ThrowsAsync` behaviour changes, parallelism configuration adjustments, and the addition of `<OutputType>Exe</OutputType>` to each test `.csproj` (required by xUnit v3's executable test host model). Optional xUnit 3 modernizations (e.g., `TheoryData<T>`, `Skip.If/Unless`) are explicitly OUT OF SCOPE for this migration.
- **FR-015**: `Assertivo` (all three test projects) MUST be updated to the latest stable release.

### Key Entities

- **Package Reference**: A NuGet dependency declared in a `.csproj` or `Directory.Build.props` file, identified by package ID and version string.
- **Test Project**: One of the three projects (`Messaggero.Tests.Unit`, `Messaggero.Tests.Contract`, `Messaggero.Tests.Integration`) that depend on xUnit and run automated tests.
- **Source Project**: One of the production projects (`Messaggero`, `Messaggero.Kafka`, `Messaggero.RabbitMQ`, `Messaggero.Testing`) whose dependencies are also upgraded but which do not directly depend on xUnit.
- **Breaking Change**: A code-level incompatibility introduced by a major version upgrade that prevents compilation or causes test behaviour to differ from the pre-migration baseline.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `dotnet build` on the full solution exits with code 0 and produces zero MSBuild warnings (including nullable reference and analyzer warnings) after all upgrades are applied. Transitive package deprecation notices from the NuGet restore graph are excluded from this criterion.
- **SC-002**: `dotnet test` passes 100% of tests across Unit, Contract, and Integration projects with no regressions compared to the pre-migration baseline. Integration tests MUST be included in this validation; Testcontainers provides the required Kafka and RabbitMQ infrastructure automatically and a Docker-capable environment is assumed to be available.
- **SC-003**: Zero package references to `xunit` 2.x or `xunit.abstractions` exist in any project file after migration.
- **SC-004**: Zero package references with a pre-release version suffix (e.g., `-preview`, `-alpha`, `-beta`, `-rc`) exist in any project file after migration.
- **SC-005**: Every package listed in the affected projects is at its latest stable release at the time the migration is completed.

## Assumptions

- "Latest stable" for each package means the highest-versioned release without a pre-release suffix available on NuGet at the time of migration.
- No new test coverage is required; the goal is to preserve all existing tests passing, not to add new tests.
- The Benchmarks project (`Messaggero.Tests.Benchmarks`) does not have xUnit as a direct dependency and only requires the `BenchmarkDotNet` upgrade.
- The `Messaggero.Testing` source project does not reference xUnit directly and has no NuGet package references of its own; it requires no changes as part of this migration.
- Any `IAsyncLifetime` usage in tests currently references the interface via xUnit rather than a separate standalone abstractions package.
- The existing parallelism configuration (if any) is intentional and must be preserved in functional intent after migrating to xUnit 3's revised defaults.
- CI pipeline configuration (if present) does not require changes beyond the package version bumps addressed in this spec.
- All upgrades (xUnit 3 migration and remaining package bumps) MUST be delivered as a single atomic commit.

## Clarifications

### Session 2026-04-26

- Q: Should `Assertivo` be formally added to the upgrade scope in the functional requirements? → A: Yes — add `Assertivo` to the FRs alongside the other test-project packages.
- Q: Should the xUnit 3 migration include optional modernizations (e.g., `TheoryData<T>`, `Skip.If/Unless`) or strictly fix breaking changes only? → A: Breaking changes only — optional modernizations are out of scope.
- Q: Should `Directory.Build.props` become the single source of truth for all package versions, or only be updated where it already centralises a version? → A: Partial — only update versions already declared in `Directory.Build.props`; project files keep their own versions.
- Q: Should the xUnit 3 migration and other dependency upgrades be delivered as a single commit or separate commits? → A: Single commit — all changes in one atomic update.
- Q: Must Integration tests pass as part of acceptance validation, or can they be skipped if Docker is unavailable? → A: Integration tests must pass — Testcontainers handles infrastructure automatically.
