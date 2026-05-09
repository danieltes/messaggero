# Feature Specification: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Feature Branch**: `00016-assertivo-upgrade-allsatisfy`  
**Created**: 2026-05-09  
**Status**: Draft  
**Input**: User description: "Assertivo 0.3.0 Upgrade and Assert.All Conversion"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Upgrade Assertivo to 0.3.0 (Priority: P1)

As a maintainer, I want all test projects on Assertivo 0.3.0 so the codebase tracks the latest stable release and gains access to the new `AllSatisfy` API.

**Why this priority**: The version upgrade is a prerequisite for the `Assert.All` conversion. Without it, the `AllSatisfy` method is unavailable and subsequent work cannot be verified.

**Independent Test**: Can be fully tested by updating the three `.csproj` files, running `dotnet build`, and confirming exit code 0 with zero warnings — before any assertion conversion is attempted.

**Acceptance Scenarios**:

1. **Given** three test projects reference `Assertivo 0.2.0`, **When** the version is updated to `0.3.0`, **Then** all projects build with exit code 0 and zero warnings.
2. **Given** existing converted assertions were written against the 0.2.0 API, **When** the version is bumped, **Then** no previously passing tests regress.

---

### User Story 2 — Convert the Two Remaining Assert.All Calls (Priority: P1)

As a maintainer, I want the two `Assert.All` occurrences in RoutingIntegrationTests.cs replaced with Assertivo equivalents so the test suite has zero remaining xUnit assertion calls.

**Why this priority**: Completing this conversion closes the migration started in `00014`, eliminates the last xUnit assertion dependency from test logic, and fulfils the full migration goal.

**Independent Test**: Can be fully tested by running the routing integration test suite and confirming all tests pass, then scanning all test `.cs` files for `Assert.` and finding zero matches.

**Acceptance Scenarios**:

1. **Given** line 80 reads `Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"))`, **When** the conversion is applied, **Then** the line reads `kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"))`.
2. **Given** line 81 reads `Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"))`, **When** the conversion is applied, **Then** the line reads `rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"))`.
3. **Given** no `Assert.*` calls remain in the Integration project, **When** the change is applied, **Then** the `using Xunit;` directive is retained because `[Fact]` attributes are still present.
4. **Given** the migration is complete, **When** the test suite is run, **Then** the routing integration test passes with identical semantics to before.

---

### Edge Cases

- `AllSatisfy` receives the same lambda body that was already inside `Assert.All`; the body already uses Assertivo's `Should()` chain. The body must not be modified — only the outer call site changes.
- If `0.3.0` introduces any breaking changes to APIs used in existing `Should()` chains, those must be resolved before the `Assert.All` conversion proceeds.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `Assertivo` MUST be updated from `0.2.0` to `0.3.0` in all three test project files: `Messaggero.Tests.Unit.csproj`, `Messaggero.Tests.Contract.csproj`, `Messaggero.Tests.Integration.csproj`.
- **FR-002**: After the version bump, the solution MUST build with exit code 0 and zero new warnings. Pre-existing `NoWarn` suppressions already present in the project files (e.g., `xUnit1051` in `Messaggero.Tests.Integration.csproj`) do not count against this criterion — only warnings introduced by this change are in scope.
- **FR-003**: All tests that passed at HEAD of `main` (as validated by the 00014 final-validation artifact) MUST continue to pass after this change. The baseline is the test run recorded in `specs/00014-xunit-assertivo-migration/artifacts/final-validation.md`.
- **FR-004**: `Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"))` in `RoutingIntegrationTests.cs` MUST be replaced with `kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"))`. The lambda body (`m => m.Type.Should().Be("OrderPlaced")`) MUST NOT be modified — only the outer call site changes.
- **FR-005**: `Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"))` in `RoutingIntegrationTests.cs` MUST be replaced with `rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"))`. The lambda body (`m => m.Type.Should().Be("EmailRequested")`) MUST NOT be modified — only the outer call site changes.
- **FR-006**: The `using Xunit;` directive in `RoutingIntegrationTests.cs` MUST be retained after conversion because xUnit test attributes remain present.
- **FR-007**: After conversion, a scan of all test `.cs` files under `tests/` MUST find zero occurrences of `Assert.` in live (non-comment) source lines. The scan uses `Select-String -Path "tests/**/*.cs" -Pattern "Assert\." -Recurse` and is expected to return zero results; string literals and comment lines that happen to contain `Assert.` do not constitute a violation, but no such occurrences are known to exist.
- **FR-008**: No xUnit package references (`xunit.v3`, `xunit.runner.visualstudio`) and no non-assertion xUnit source code MUST be changed as part of this feature.
- **FR-009**: No production source files MUST be modified. Production source files are defined as all files under `src/` in the repository root, including `.cs`, `.csproj`, `.props`, and `.targets` files.

### Conversion Catalog

| Location | Before | After |
|----------|--------|-------|
| RoutingIntegrationTests.cs | `Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"))` | `kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"))` |
| RoutingIntegrationTests.cs | `Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"))` | `rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"))` |

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All three test project `.csproj` files reference `Assertivo 0.3.0`.
- **SC-002**: `dotnet build` exits with code 0 and zero new warnings after the version bump (pre-existing suppressed warnings excluded; see FR-002).
- **SC-003**: `dotnet test` is green across Unit, Contract, and Integration projects after both the version bump and the `Assert.All` conversion.
- **SC-004**: Zero occurrences of `Assert.` remain in any test `.cs` file — the full xUnit assertion surface is cleared.
- **SC-005**: Non-exact candidate artifacts from `00014` (`NC-0001`, `NC-0002`) are updated to `Resolved` status.

## Out of Scope

- Updating `xunit.v3` or `xunit.runner.visualstudio` versions.
- Any source file changes outside the three `.csproj` files and `RoutingIntegrationTests.cs`.
- Removing xUnit dependencies (all three projects still require them for test attributes and runner).

## Assumptions

- `Assertivo 0.3.0` is available on NuGet at the time of implementation.
- The `AllSatisfy` API in 0.3.0 accepts the same lambda signature already present inside the existing `Assert.All` calls, requiring no modification to the lambda body.
- The only files requiring changes are the three test `.csproj` files and `RoutingIntegrationTests.cs`.
- The `using Xunit;` directive covers both xUnit test attributes (`[Fact]`, `[Theory]`) and was the source of the `Assert.*` namespace; removing `Assert.` calls does not make the directive unused because attributes remain.
- Non-exact candidates `NC-0001` and `NC-0002` recorded in `00014` correspond exactly to the two `Assert.All` calls addressed here.
- Broker-agnostic contract, broker-specific deviations, error handling, retry behavior, and delivery semantics: N/A — this is a test tooling change only. No production message-handling code is modified.
