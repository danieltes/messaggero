# Feature Specification: Replace FluentAssertions with Assertivo

**Feature Branch**: `00003-fluentassertions-to-assertivo`  
**Created**: 2026-04-18  
**Status**: Draft  
**Input**: User description: "Replace FluentAssertions with Assertivo based on the issue description for migrating all test assertions from FluentAssertions to Assertivo — a MIT-licensed, fluent, strongly-typed assertion library for .NET with a compatible .Should() API surface and AOT compatibility."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Full Test Suite Passes After Library Swap (Priority: P1)

As a contributor, I want to run the full test suite after replacing the assertion library so that I can verify zero regressions and continue developing with confidence.

**Why this priority**: The entire migration is meaningless if the test suite breaks. This is the core deliverable — all existing tests must continue to pass identically after swapping the underlying assertion library.

**Independent Test**: Run the complete test suite (`dotnet test`) across all three test projects (Unit, Integration, Contract) and confirm all tests pass with zero failures.

**Acceptance Scenarios**:

1. **Given** all FluentAssertions package references have been replaced with Assertivo, **When** a contributor runs `dotnet test` on the solution, **Then** every test that previously passed continues to pass with zero failures or skips.
2. **Given** the migration is complete, **When** a contributor reviews the test output, **Then** no warnings or errors related to missing assertion methods appear.

---

### User Story 2 - Package References Updated Across All Test Projects (Priority: P1)

As a project maintainer, I want the FluentAssertions NuGet package replaced with Assertivo in every test project so that the project no longer depends on a commercially-licensed library.

**Why this priority**: Removing the commercial dependency is the primary motivation for this migration. Without this change, the project remains exposed to licensing risk.

**Independent Test**: Run `dotnet list package` on the solution and confirm FluentAssertions does not appear in any project, while Assertivo is listed in all test projects that previously referenced FluentAssertions.

**Acceptance Scenarios**:

1. **Given** the three test projects (Unit, Integration, Contract) reference FluentAssertions, **When** the migration is applied, **Then** each project references Assertivo instead.
2. **Given** the migration is complete, **When** a maintainer runs `dotnet list package` across the solution, **Then** FluentAssertions does not appear anywhere in the output.
3. **Given** the benchmark project does not use FluentAssertions, **When** the migration is applied, **Then** the benchmark project remains unchanged.

---

### User Story 3 - Using Directives Updated in All Test Files (Priority: P1)

As a contributor, I want all `using FluentAssertions;` directives replaced with `using Assertivo;` so that the code compiles cleanly against the new library.

**Why this priority**: This is a prerequisite for compilation. Without updating the imports, no test file will compile after the package swap.

**Independent Test**: Search the entire codebase for `using FluentAssertions` and confirm zero matches. Search for `using Assertivo` and confirm it appears in all 18 test files that previously imported FluentAssertions.

**Acceptance Scenarios**:

1. **Given** 18 test files contain `using FluentAssertions;`, **When** the migration is applied, **Then** each file contains `using Assertivo;` instead.
2. **Given** the migration is complete, **When** a contributor searches the codebase for the string `FluentAssertions`, **Then** zero results are returned from source files.

---

### User Story 4 - Assertion Compatibility Verified for All Patterns (Priority: P2)

As a contributor, I want confirmation that every assertion pattern used in the test suite is supported by Assertivo so that no test logic needs to be rewritten.

**Why this priority**: If Assertivo lacks coverage for certain assertion patterns (e.g., `.ContainSingle().Which`, `.ThrowAsync<>()`, `.AllSatisfy()`), those tests will need alternative approaches. This must be verified to scope the migration effort accurately.

**Independent Test**: Compile and run each test project individually after the swap. Any unsupported assertion pattern will produce a compile error or runtime failure, identifying the gap.

**Acceptance Scenarios**:

1. **Given** the test suite uses boolean, string, numeric, collection, exception, async exception, and drill-down assertions, **When** Assertivo replaces FluentAssertions, **Then** all assertion patterns compile and behave identically.
2. **Given** an assertion pattern is not supported by Assertivo, **When** a contributor encounters a compilation error, **Then** the pattern is rewritten using a native xUnit assert so the migration is not blocked.

---

### Edge Cases

- What happens if Assertivo's `.BeEquivalentTo()` uses different comparison semantics (e.g., ordering sensitivity) than FluentAssertions?
- How are chained assertions like `.ContainSingle().Which.Property.Should().Be(...)` handled if the intermediate `.Which` accessor differs in behavior?
- What happens if Assertivo's exception assertion `.Throw<T>()` returns a different continuation type, breaking `.Which.Property` chains?
- How does the migration handle any FluentAssertions-specific extension methods or custom assertion extensions defined in the project?

## Clarifications

### Session 2026-04-18

- Q: Incompatible assertion pattern resolution strategy? → A: Rewrite using xUnit native asserts immediately — do not block on upstream.
- Q: Assertivo version pinning strategy for pre-1.0 library? → A: Pin to exact version 0.1.2 — update manually after testing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: All test projects that reference FluentAssertions MUST have that package reference removed and replaced with an Assertivo package reference pinned to exact version `0.1.2`.
- **FR-002**: All `using FluentAssertions;` directives across the codebase MUST be replaced with `using Assertivo;`.
- **FR-003**: The full test suite MUST pass with zero regressions after the migration (same number of passing tests before and after).
- **FR-004**: No `.cs` or `.csproj` file in the repository MUST contain any remaining reference to FluentAssertions after the migration is complete. Specification artifacts under `specs/` are exempt.
- **FR-005**: Projects that do not reference FluentAssertions (e.g., Benchmarks, source projects) MUST remain unchanged.
- **FR-006**: Any assertion pattern not supported by Assertivo MUST be rewritten using xUnit native asserts immediately — the migration MUST NOT be blocked waiting for upstream library support.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of previously passing tests continue to pass after the migration with zero new failures or skips.
- **SC-002**: Zero references to `FluentAssertions` remain in any `.cs`, `.csproj`, or `.props` file across the repository. Specification artifacts under `specs/` are exempt.
- **SC-003**: All three test projects (Unit, Integration, Contract) successfully compile and run against the Assertivo library.
- **SC-004**: The migration introduces no new compile-time warnings related to assertion usage.
- **SC-005**: The project is fully compliant with MIT-only dependencies for its assertion library, eliminating commercial licensing risk.

## Risks

- **Pre-1.0 library**: Assertivo 0.1.2 is pre-1.0 and may introduce breaking API changes in future versions. Mitigated by pinning to exact version `0.1.2` and requiring manual update after testing (see [research.md](research.md) for full compatibility matrix).
- **API surface drift**: If Assertivo deprecates or alters `.Should()` chain semantics in a future release, tests may require updates. Mitigated by version pinning and the fallback strategy in FR-006.

## Assumptions

- Assertivo v0.1.2 provides a `.Should()` API surface compatible with the assertion patterns currently used in this project (boolean, string, numeric, collection, exception, async exception, and drill-down assertions). The package reference MUST be pinned to exact version `0.1.2` and updated manually after verification.
- The Assertivo library targets .NET 10.0+, which is compatible with the project's current target framework.
- The benchmark project (Messaggero.Tests.Benchmarks) does not use FluentAssertions and is out of scope for this migration.
- No custom FluentAssertions extensions or assertion plugins are defined in the codebase — the migration is limited to standard FluentAssertions API usage.
- If a small number of assertion patterns are unsupported by Assertivo, they MUST be rewritten using native xUnit assertions immediately rather than blocking on upstream support.
- Constitution requirements for error handling, retry behavior, delivery semantics, and ordering expectations (Article I.2) are not applicable — this feature modifies test infrastructure only, with no runtime behavior changes.
