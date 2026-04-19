# Tasks: Replace FluentAssertions with Assertivo

**Input**: Design documents from `/specs/00003-fluentassertions-to-assertivo/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Phase 1: Setup

**Purpose**: Record pre-migration baseline to validate zero regressions

- [X] T001 Run `dotnet test` and record total passing test count as the pre-migration baseline
- [X] T002 Run `dotnet list package` and confirm FluentAssertions 8.3.0 is listed in the three test projects

---

## Phase 2: User Story 2 — Package References Updated (Priority: P1)

**Goal**: Replace FluentAssertions NuGet package with Assertivo 0.1.2 in all three test projects

**Independent Test**: Run `dotnet list package` and confirm FluentAssertions no longer appears; Assertivo 0.1.2 is listed for Unit, Integration, and Contract projects

- [X] T003 [P] [US2] Replace `<PackageReference Include="FluentAssertions" Version="8.3.0" />` with `<PackageReference Include="Assertivo" Version="0.1.2" />` in tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj
- [X] T004 [P] [US2] Replace `<PackageReference Include="FluentAssertions" Version="8.3.0" />` with `<PackageReference Include="Assertivo" Version="0.1.2" />` in tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj
- [X] T005 [P] [US2] Replace `<PackageReference Include="FluentAssertions" Version="8.3.0" />` with `<PackageReference Include="Assertivo" Version="0.1.2" />` in tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj
- [X] T006 [US2] Run `dotnet restore` and verify all three projects restore successfully with the Assertivo package

**Checkpoint**: Package references swapped. `dotnet list package` shows Assertivo 0.1.2 in all three test projects, zero FluentAssertions references.

---

## Phase 3: User Story 3 — Using Directives Updated (Priority: P1)

**Goal**: Replace all `using FluentAssertions;` directives with `using Assertivo;` across 18 test files

**Independent Test**: Search codebase for `using FluentAssertions` — zero matches. Search for `using Assertivo` — 18 matches in test files.

### Unit Test Project (7 files)

- [X] T007 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Configuration/ScopedHandlerValidationTests.cs
- [X] T008 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Examples/HandlerIsolationExampleTests.cs
- [X] T009 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Hosting/HandlerDispatcherTests.cs
- [X] T010 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Hosting/MessageBusTests.cs
- [X] T011 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Hosting/RetryExecutorTests.cs
- [X] T012 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Observability/LogScrubTests.cs
- [X] T013 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Unit/Routing/RoutingTableTests.cs

### Integration Test Project (6 files)

- [X] T014 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/AdapterIsolationTests.cs
- [X] T015 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/FanInDegradedTests.cs
- [X] T016 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/FanInIntegrationTests.cs
- [X] T017 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/FanOutFailureTests.cs
- [X] T018 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs
- [X] T019 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Integration/ScopedSubscriptionTests.cs

### Contract Test Project (5 files)

- [X] T020 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Contract/AckNackContractTests.cs
- [X] T021 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Contract/BackpressureContractTests.cs
- [X] T022 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Contract/LifecycleContractTests.cs
- [X] T023 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Contract/PublishContractTests.cs
- [X] T024 [P] [US3] Replace `using FluentAssertions;` with `using Assertivo;` in tests/Messaggero.Tests.Contract/SubscribeContractTests.cs

**Checkpoint**: All 18 using directives replaced. Codebase search for `using FluentAssertions` returns zero results.

- [X] T024a [US3] Search all `GlobalUsings.cs`, `Usings.cs`, and `Directory.Build.props` files for `global using FluentAssertions` and replace with `global using Assertivo;` if found
- [X] T024b [US3] Search all `.cs` files for sub-namespace imports matching `using FluentAssertions.*` (e.g., `using FluentAssertions.Execution;`, `using FluentAssertions.Primitives;`) and replace or remove as appropriate

---

## Phase 4: User Story 4 — Assertion Compatibility Verified (Priority: P2)

**Goal**: Confirm all assertion patterns compile and behave identically under Assertivo

**Independent Test**: `dotnet build` succeeds with zero errors and zero warnings; each test project compiles independently

- [X] T024c Run `dotnet clean` on the solution to purge stale FluentAssertions assemblies from `bin/` and `obj/` directories
- [X] T025 [US4] Run `dotnet build` on the full solution and verify zero compilation errors and zero new warnings (TreatWarningsAsErrors is enabled)
- [X] T026 [US4] If any assertion pattern fails to compile, rewrite it using native xUnit `Assert.*` methods in the affected file (per FR-006)

**Checkpoint**: Solution compiles cleanly. All assertion patterns resolve against Assertivo API.

---

## Phase 5: User Story 1 — Full Test Suite Passes (Priority: P1)

**Goal**: Validate zero regressions — every previously passing test continues to pass

**Independent Test**: `dotnet test` passes with the same test count recorded in T001

- [X] T027 [US1] Run `dotnet test` on the full solution and verify all tests pass with zero failures and zero skips
- [X] T028 [US1] Compare post-migration passing test count against the baseline recorded in T001 to confirm zero regressions

**Checkpoint**: Full test suite green. Same test count as pre-migration baseline.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [X] T029 [P] Run `dotnet list package` on the solution and confirm zero FluentAssertions references remain in any project
- [X] T030 [P] Search all `.cs` and `.csproj` files for the string `FluentAssertions` and confirm zero matches (exclude `specs/` directory)
- [X] T031 Verify the benchmark project (tests/Messaggero.Tests.Benchmarks/) is unchanged — no new dependencies or file modifications
- [X] T032 Run quickstart.md validation: confirm all 6 migration steps from quickstart.md have been completed successfully

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **US2 — Package References (Phase 2)**: Depends on Setup — BLOCKS all using directive changes
- **US3 — Using Directives (Phase 3)**: Depends on Phase 2 — packages must be swapped before imports
- **US4 — Compilation Check (Phase 4)**: Depends on Phase 3 — all files must be updated before building
- **US1 — Test Suite (Phase 5)**: Depends on Phase 4 — code must compile before tests can run
- **Polish (Phase 6)**: Depends on Phase 5 — all tests must pass before final validation

### User Story Dependencies

- **US2 (Package References)**: Can start after Setup — no dependencies on other stories
- **US3 (Using Directives)**: Depends on US2 — packages must be available before imports compile
- **US4 (Compatibility)**: Depends on US3 — all imports must resolve for compilation
- **US1 (Test Suite)**: Depends on US4 — code must compile before tests can execute

### Within Each Phase

- Phase 2: T003, T004, T005 are parallel [P] — different `.csproj` files; T006 waits for all three
- Phase 3: All 18 tasks (T007–T024) are parallel [P] — each is a different `.cs` file
- Phase 4: T025 runs first; T026 only runs if T025 fails (conditional)
- Phase 5: T027 runs first; T028 compares against baseline
- Phase 6: T029, T030 are parallel [P]; T031, T032 are sequential

### Parallel Opportunities

```text
# Phase 2 — all three .csproj edits in parallel:
T003: Messaggero.Tests.Unit.csproj
T004: Messaggero.Tests.Integration.csproj
T005: Messaggero.Tests.Contract.csproj

# Phase 3 — all 18 using directive replacements in parallel:
T007–T013: Unit project (7 files)
T014–T019: Integration project (6 files)
T020–T024: Contract project (5 files)

# Phase 6 — validation checks in parallel:
T029: dotnet list package check
T030: codebase string search
```

---

## Implementation Strategy

### MVP First (Linear Execution)

1. Complete Phase 1: Setup (record baseline)
2. Complete Phase 2: Swap package references (3 files)
3. Complete Phase 3: Replace using directives (18 files)
4. Complete Phase 4: Verify compilation
5. Complete Phase 5: Run full test suite — **STOP and VALIDATE**
6. Complete Phase 6: Polish and final checks

### Incremental Delivery

1. Setup + Package swap + Using directives → Code compiles
2. Compilation verified → No API gaps found
3. Test suite passes → **Migration complete, zero regressions**
4. Final validation → **Clean codebase, no FluentAssertions traces**
