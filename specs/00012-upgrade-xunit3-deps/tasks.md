# Tasks: Upgrade xUnit 3 and Dependencies

**Input**: Design documents from `/specs/00012-upgrade-xunit3-deps/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, quickstart.md ✅

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to ([US1], [US2])
- Exact file paths included in all task descriptions

---

## Phase 1: Setup

**Purpose**: Verify pre-migration baseline so regressions can be detected after upgrades

- [X] T001 Verify current solution builds and record baseline test pass counts by running `dotnet build` and `dotnet test` across `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Contract/`, and `tests/Messaggero.Tests.Integration/`

---

> **Note — Phase 2 omitted**: No foundational shared prerequisite tasks exist for this feature. US1 and US2 edit entirely separate files and can both start immediately after Phase 1.

## Phase 3: User Story 1 — Migrate Test Projects to xUnit 3 (Priority: P1) 🎯 MVP

**Goal**: All three test projects reference `xunit.v3` 3.2.2 (replacing `xunit` 2.9.3), are declared as executable projects, and pass all tests with zero regressions.

**Independent Test**: Run `dotnet test` across the three test projects. All previously passing tests must pass. No `xunit` 2.x or `xunit.abstractions` package references may remain in any `.csproj`.

### Implementation for User Story 1

- [X] T002 [P] [US1] Migrate `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj`: remove `<PackageReference Include="xunit" Version="2.9.3" />`, add `<PackageReference Include="xunit.v3" Version="3.2.2" />`, add `<OutputType>Exe</OutputType>` to the `<PropertyGroup>`, and update `xunit.runner.visualstudio` to `3.1.5`, `Microsoft.NET.Test.Sdk` to `18.5.0`, `Assertivo` to `0.2.0`, `Microsoft.Extensions.DependencyInjection` to `10.0.7`
- [X] T003 [P] [US1] Migrate `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj`: remove `<PackageReference Include="xunit" Version="2.9.3" />`, add `<PackageReference Include="xunit.v3" Version="3.2.2" />`, add `<OutputType>Exe</OutputType>` to the `<PropertyGroup>`, and update `xunit.runner.visualstudio` to `3.1.5`, `Microsoft.NET.Test.Sdk` to `18.5.0`, `Assertivo` to `0.2.0`
- [X] T004 [P] [US1] Migrate `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`: remove `<PackageReference Include="xunit" Version="2.9.3" />`, add `<PackageReference Include="xunit.v3" Version="3.2.2" />`, add `<OutputType>Exe</OutputType>` to the `<PropertyGroup>`, and update `xunit.runner.visualstudio` to `3.1.5`, `Microsoft.NET.Test.Sdk` to `18.5.0`, `Assertivo` to `0.2.0`, `Microsoft.Extensions.DependencyInjection` to `10.0.7`, `Testcontainers.Kafka` to `4.11.0`, `Testcontainers.RabbitMq` to `4.11.0`
- [X] T005 [US1] Run `dotnet restore` on the solution and confirm all packages resolve without errors (depends on T002, T003, T004)
- [X] T006 [US1] Run `dotnet build --no-restore` on the solution and confirm exit code 0 with zero warnings across the three migrated test projects (depends on T005)
- [X] T007 [US1] Run `dotnet test --no-build` across `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Contract/`, and `tests/Messaggero.Tests.Integration/` with Docker running; confirm 100% pass rate matching the Phase 1 baseline (depends on T006)

**Checkpoint**: User Story 1 is fully functional and independently testable. xUnit 3 migration is complete.

---

## Phase 4: User Story 2 — Upgrade All Other Stale Dependencies (Priority: P2)

**Goal**: All source project dependencies and the benchmarks project are updated to their latest stable releases. No pre-release version suffixes remain anywhere in the solution.

**Independent Test**: Run `dotnet build` and `dotnet test` on the full solution after source project changes. All tests pass and `dotnet list package` shows every package at its target version with no `-preview` suffix.

### Implementation for User Story 2

- [X] T008 [P] [US2] Update `src/Messaggero/Messaggero.csproj`: change all four `Microsoft.Extensions.*` packages from `10.0.0-preview.3.25171.5` to `10.0.7` (`DependencyInjection.Abstractions`, `Logging.Abstractions`, `Options`, `Hosting.Abstractions`)
- [X] T009 [P] [US2] Update `src/Messaggero.Kafka/Messaggero.Kafka.csproj`: change `Confluent.Kafka` from `2.8.0` to `2.14.0`
- [X] T010 [P] [US2] Update `src/Messaggero.RabbitMQ/Messaggero.RabbitMQ.csproj`: change `RabbitMQ.Client` from `7.1.2` to `7.2.1`
- [X] T011 [P] [US2] Update `tests/Messaggero.Tests.Benchmarks/Messaggero.Tests.Benchmarks.csproj`: change `BenchmarkDotNet` from `0.14.0` to `0.15.8`
- [X] T012 [US2] Run `dotnet restore` on the solution and confirm all packages resolve without errors (depends on T008, T009, T010, T011)
- [X] T013 [US2] Run `dotnet build --no-restore` on the full solution and confirm exit code 0 with zero warnings across all source and test projects (including `tests/Messaggero.Tests.Benchmarks/`; depends on T012)
- [X] T014 [P] [US2] Run `dotnet test --no-build` across `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Contract/`, and `tests/Messaggero.Tests.Integration/` with Docker running; confirm 100% pass rate with no regressions (depends on T013; can run in parallel with T020)
- [X] T020 [P] [US2] Run `dotnet run -c Release --project tests/Messaggero.Tests.Benchmarks/ -- --job Dry --filter '*'` and confirm exit code 0; dry-run mode verifies BenchmarkDotNet 0.15.8 initialises and executes the benchmark harness without performing actual measurements (depends on T013; can run in parallel with T014)

**Checkpoint**: User Story 2 complete. All source projects use stable, up-to-date dependencies and the benchmarks project is validated at runtime.

---

## Phase 5: Polish & Final Validation

**Purpose**: Cross-cutting verification that all success criteria are satisfied before the single atomic commit

- [X] T015 [P] Search all `.csproj` files in the solution for any package version string containing `-preview`, `-alpha`, `-beta`, or `-rc` and confirm zero matches (SC-004)
- [X] T016 [P] Search all `.csproj` files in the solution for `xunit" Version` and `xunit.abstractions` and confirm zero matches (SC-003)
- [X] T017 [P] Run `dotnet list package` on the solution and verify every resolved version matches the target table in `specs/00012-upgrade-xunit3-deps/research.md` §10 (SC-005)
- [X] T021 [P] Run `dotnet list package --vulnerable` on the solution and confirm zero known CVEs or security advisories across all directly referenced packages (CHK034; can run in parallel with T015, T016, T017)
- [X] T018 Run `dotnet build` on the full solution and confirm exit code 0 and zero warnings (SC-001; depends on T015, T016, T017, T021)
- [X] T019 Run `dotnet test` across `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Contract/`, and `tests/Messaggero.Tests.Integration/` with Docker running and confirm 100% pass rate (SC-002; depends on T018)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 3)**: Depends on Phase 1 completion — can start immediately after T001
- **User Story 2 (Phase 4)**: Independent of User Story 1 — can start in parallel with Phase 3 after T001
- **Polish (Phase 5)**: Depends on both Phase 3 and Phase 4 completion

### User Story Dependencies

- **User Story 1 (P1)**: T002, T003, T004 can all run in parallel (different files). T005 → T006 → T007 are sequential.
- **User Story 2 (P2)**: T008, T009, T010, T011 can all run in parallel (different files). T012 → T013 → T014 are sequential.
- **US1 and US2 are fully independent**: they touch completely separate files and can be worked in parallel.

### Task Dependency Graph

```
T001
├── T002 ─┐
├── T003 ─┼─ T005 → T006 → T007 ─┐
├── T004 ─┘                       │
├── T008 ─┐                       ├─ T015 ─┐
├── T009 ─┼─ T012 → T013 → T014 ─┤   T016 ─┼─ T018 → T019
├── T010 ─┘                       │   T017 ─┤
└── T011 ─┘                       └─  T021 ─┘
```

---

## Parallel Execution Example: User Story 1

Three test project files can be edited simultaneously — each is an independent file with no cross-dependency:

```
Agent/Developer A:          Agent/Developer B:          Agent/Developer C:
T002 (Unit.csproj)          T003 (Contract.csproj)      T004 (Integration.csproj)
        \                           |                           /
         \                          |                          /
          └──────────── T005 (dotnet restore) ───────────────┘
                                    |
                               T006 (dotnet build)
                                    |
                               T007 (dotnet test)
```

## Parallel Execution Example: User Story 2

All four source/benchmarks files can be edited simultaneously:

```
Agent/Developer A:      Agent/Developer B:      Agent/Developer C:      Agent/Developer D:
T008 (Messaggero)       T009 (Kafka)            T010 (RabbitMQ)         T011 (Benchmarks)
        \                     |                       |                       /
         \                    |                       |                      /
          └─────────────── T012 (dotnet restore) ─────────────────────────┘
                                       |
                               T013 (dotnet build)
                                       |
                               T014 (dotnet test)
```

---

## Implementation Strategy

### MVP Scope

**User Story 1 (P1) alone** is the MVP — it resolves the version mismatch between `xunit` 2.9.3 and `xunit.runner.visualstudio` 3.0.2 and puts the solution on the actively maintained xUnit 3 line. Running `dotnet test` green across all three test projects after Phase 3 is a fully shippable outcome.

### Incremental Delivery

1. Complete Phase 1 (T001) — record baseline
2. Work Phase 3 and Phase 4 in parallel (all `.csproj` edits are independent files)
3. Run restore, build, test for each story independently to confirm no regressions introduced
4. Run Phase 5 validation gates
5. Commit all changes as a single atomic commit per the delivery constraint

### Risk-Ordered Approach Within Phase 3

Within US1, the ordering of csproj edits does not matter (all parallel), but validate in this risk order:
1. **Unit tests first** (T006/T007 scope) — smallest test surface, fastest feedback
2. **Contract tests** — no external infrastructure needed
3. **Integration tests** — requires Docker; highest risk due to Testcontainers version bump alongside xUnit migration

### No C# Source Changes Expected

Research confirmed zero breaking changes requiring C# source edits in this codebase:
- No `IAsyncLifetime` usages
- No `CollectionBehavior` / parallelism attributes
- No `xunit.abstractions` imports
- The single `Assert.ThrowsAsync<T>` call in `MessageBusTests.cs:96` is xUnit v3 compatible

All 21 tasks are `.csproj` file edits or validation/verification steps.

---

## Task Count Summary

| Phase | Tasks | Parallelizable | Story |
|---|---|---|---|
| Phase 1: Setup | 1 (T001) | 0 | — |
| Phase 3: User Story 1 | 6 (T002–T007) | 3 (T002, T003, T004) | US1 |
| Phase 4: User Story 2 | 8 (T008–T014, T020) | 5 (T008, T009, T010, T011, T020) | US2 |
| Phase 5: Polish | 6 (T015–T019, T021) | 4 (T015, T016, T017, T021) | — |
| **Total** | **21** | **12** | |

**Parallel opportunities**: 12 of 21 tasks (57%) can run in parallel within their phase.

> **Note on intentional build/test repetition**: `dotnet build` and `dotnet test` appear three times each (US1 gates T006/T007, US2 gates T013/T014, and Polish gates T018/T019). This is deliberate — each story's isolated validation catches regressions before stories are combined in Phase 5, minimising debugging effort if an upgrade introduces an unexpected failure.

**Independent test criteria**:
- US1: `dotnet test` green across Unit + Contract + Integration after Phase 3; zero `xunit` 2.x refs
- US2: `dotnet build` + `dotnet test` green after Phase 4; zero `-preview` suffixes in source projects

**Suggested MVP**: Phase 3 (User Story 1) alone — resolves the version mismatch and moves to xUnit 3 in one focused increment.
