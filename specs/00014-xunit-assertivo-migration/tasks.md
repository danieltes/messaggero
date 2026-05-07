# Tasks: Test Assertion Modernization

**Input**: Design documents from `/specs/00014-xunit-assertivo-migration/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: This feature is test-focused and explicitly requires staged build/test validation. Validation tasks are included for each user story.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: User story label ([US1], [US2], [US3])
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish reproducible baseline evidence and artifact workspace before code edits

- [X] T001 Create artifact workspace directories `specs/00014-xunit-assertivo-migration/artifacts/` and `specs/00014-xunit-assertivo-migration/artifacts/scripts/`
- [X] T002 Run baseline validation commands from `specs/00014-xunit-assertivo-migration/quickstart.md` Step 1 and record outputs in `specs/00014-xunit-assertivo-migration/artifacts/baseline-validation.md`
- [X] T003 [P] Capture baseline xUnit assertion inventory from `tests/**/*.cs` and save file/line inventory in `specs/00014-xunit-assertivo-migration/artifacts/assertion-inventory.md`
- [X] T004 [P] Capture baseline xUnit package and namespace inventory from `tests/**/*.csproj` and `tests/**/*.cs` in `specs/00014-xunit-assertivo-migration/artifacts/xunit-inventory.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build automation and guardrails required before user-story conversion work

**Critical**: User story implementation starts only after these tasks complete

- [X] T005 Implement assertion classification scanner script in `specs/00014-xunit-assertivo-migration/artifacts/scripts/scan-assertions.ps1` using rules from `specs/00014-xunit-assertivo-migration/contracts/assertion-mapping-contract.md`
- [X] T006 Implement non-exact artifact generator script in `specs/00014-xunit-assertivo-migration/artifacts/scripts/generate-non-exact-artifacts.ps1` that produces `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json` and `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md`
- [X] T007 Implement scope guard script in `specs/00014-xunit-assertivo-migration/artifacts/scripts/verify-scope.ps1` to reject out-of-scope file modifications outside test projects and feature docs/artifacts
- [X] T008 Run script dry-run verification and record command examples/results in `specs/00014-xunit-assertivo-migration/artifacts/script-dry-run.md`

**Checkpoint**: Automation and guardrails are ready; user stories can begin

---

## Phase 3: User Story 1 - Migrate Supported Assertions Safely (Priority: P1)

**Goal**: Replace supported exact xUnit assertions with equivalent Assertivo assertions while preserving semantics and test behavior

**Independent Test**: Build and test touched projects after conversion; verify converted lines follow exact mapping rules and outcomes match baseline

### Validation for User Story 1

- [X] T009 [US1] Run targeted staged validation for changed projects and record results in `specs/00014-xunit-assertivo-migration/artifacts/us1-validation.md` using `dotnet build` and `dotnet test` for `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj`, `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`, and `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj`

### Implementation for User Story 1

- [X] T010 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Contract/AckNackContractTests.cs` and `tests/Messaggero.Tests.Contract/BackpressureContractTests.cs`
- [X] T011 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Contract/LifecycleContractTests.cs` and `tests/Messaggero.Tests.Contract/PublishContractTests.cs`
- [X] T012 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Integration/AdapterIsolationTests.cs` and `tests/Messaggero.Tests.Integration/FanInIntegrationTests.cs`
- [X] T013 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Integration/FanOutFailureTests.cs` and supported-only lines in `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs`
- [X] T014 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Unit/Configuration/ScopedHandlerValidationTests.cs` and `tests/Messaggero.Tests.Unit/Hosting/HandlerDispatcherTests.cs`
- [X] T015 [P] [US1] Convert supported assertions in `tests/Messaggero.Tests.Unit/Hosting/MessageBusTests.cs` and `tests/Messaggero.Tests.Unit/Routing/RoutingTableTests.cs`
- [X] T016 [US1] Normalize imports in all touched files under `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Integration/`, and `tests/Messaggero.Tests.Contract/` to satisfy `using Assertivo;` and retained `using Xunit;` constraints
- [X] T017 [US1] Record semantic-equivalence review sample (minimum 30 converted assertions) in `specs/00014-xunit-assertivo-migration/artifacts/semantic-equivalence-sample.md`

**Checkpoint**: Supported exact mappings are converted and independently validated

---

## Phase 4: User Story 2 - Preserve Unsupported and Ambiguous Assertions (Priority: P2)

**Goal**: Keep non-exact, ambiguous, and unsupported assertions unchanged while generating required manual-review artifacts

**Independent Test**: Confirm non-exact assertions remain unchanged and both JSON/Markdown review artifacts are generated with matching candidate IDs and counts

### Validation for User Story 2

- [X] T018 [US2] Run integration-focused mixed-pattern validation and store results in `specs/00014-xunit-assertivo-migration/artifacts/us2-validation.md` using `dotnet test tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`

### Implementation for User Story 2

- [X] T019 [US2] Run `specs/00014-xunit-assertivo-migration/artifacts/scripts/scan-assertions.ps1` and export full post-conversion occurrence dataset to `specs/00014-xunit-assertivo-migration/artifacts/assertion-occurrences.json`
- [X] T020 [P] [US2] Generate machine-readable non-exact candidate artifact `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json` using `specs/00014-xunit-assertivo-migration/artifacts/scripts/generate-non-exact-artifacts.ps1`
- [X] T021 [P] [US2] Generate human-readable non-exact candidate artifact `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md` using `specs/00014-xunit-assertivo-migration/artifacts/scripts/generate-non-exact-artifacts.ps1`
- [X] T022 [US2] Verify candidate-count and candidate-ID consistency between `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json` and `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md`, then record evidence in `specs/00014-xunit-assertivo-migration/artifacts/non-exact-artifact-consistency.md`
- [X] T023 [US2] Manually confirm `Assert.All(...)` remains unchanged in `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs` and capture review notes in `specs/00014-xunit-assertivo-migration/artifacts/non-exact-review-notes.md`

**Checkpoint**: Non-exact handling is complete and auditable

---

## Phase 5: User Story 3 - Enforce Refactor Scope Boundaries (Priority: P3)

**Goal**: Enforce test-only modification scope and xUnit dependency retention/removal policy per project

**Independent Test**: Verify no out-of-scope files changed; verify xUnit dependency decisions are documented and validated per project

### Validation for User Story 3

- [X] T024 [US3] Execute scope guard and write scope pass/fail evidence to `specs/00014-xunit-assertivo-migration/artifacts/scope-audit.md` using `specs/00014-xunit-assertivo-migration/artifacts/scripts/verify-scope.ps1`

### Implementation for User Story 3

- [X] T025 [P] [US3] Scan xUnit namespace and attribute usage in `tests/Messaggero.Tests.Unit/`, `tests/Messaggero.Tests.Integration/`, and `tests/Messaggero.Tests.Contract/` and record per-project evidence in `specs/00014-xunit-assertivo-migration/artifacts/xunit-usage-audit.md`
- [X] T026 [P] [US3] Evaluate dependency retention/removal criteria for `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj`, `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`, and `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj`; record decision matrix in `specs/00014-xunit-assertivo-migration/artifacts/xunit-dependency-decisions.md`
- [X] T027 [US3] Apply qualifying xUnit dependency removals in affected test `.csproj` files or explicitly record keep decisions in `specs/00014-xunit-assertivo-migration/artifacts/xunit-dependency-decisions.md` when removal criteria are not met
- [X] T028 [US3] Run compile/test validation for any dependency-changed project and save evidence in `specs/00014-xunit-assertivo-migration/artifacts/xunit-dependency-validation.md`
- [X] T029 [US3] Re-run `specs/00014-xunit-assertivo-migration/artifacts/scripts/verify-scope.ps1` and append final boundary status to `specs/00014-xunit-assertivo-migration/artifacts/scope-audit.md`

**Checkpoint**: Scope and dependency governance are complete

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final end-to-end validation and release-readiness evidence across all stories

- [X] T030 [P] Run full completion gate commands and record outputs in `specs/00014-xunit-assertivo-migration/artifacts/final-validation.md` using `dotnet build Messaggero.slnx` and `dotnet test Messaggero.slnx`
- [X] T031 [P] Validate all quickstart workflow steps from `specs/00014-xunit-assertivo-migration/quickstart.md` and capture step-by-step pass/fail in `specs/00014-xunit-assertivo-migration/artifacts/quickstart-validation.md`
- [X] T032 [P] Compute success criteria evidence for SC-001 through SC-009 and record pass/fail with references in `specs/00014-xunit-assertivo-migration/artifacts/success-criteria-report.md`
- [X] T033 Reconcile final artifact links and update execution notes in `specs/00014-xunit-assertivo-migration/quickstart.md` if command flow changed during implementation
- [X] T034 Run final release-readiness checklist and capture outcome in `specs/00014-xunit-assertivo-migration/artifacts/release-readiness.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies; start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user story implementation
- **Phase 3 (US1)**: Depends on Phase 2 completion
- **Phase 4 (US2)**: Depends on Phase 3 completion (uses post-conversion outputs)
- **Phase 5 (US3)**: Depends on Phase 3 completion; can run in parallel with Phase 4 after conversion is complete
- **Phase 6 (Polish)**: Depends on completion of Phases 4 and 5

### User Story Dependencies

- **US1 (P1)**: MVP story; no dependency on other user stories
- **US2 (P2)**: Depends on converted code from US1 for final non-exact candidate output
- **US3 (P3)**: Depends on conversion completion from US1; governance checks can proceed alongside US2 artifact generation

### Within Each User Story

- Validation task runs before story implementation edits
- File conversion tasks marked [P] can execute in parallel
- Import normalization and story evidence tasks run after conversion tasks
- Story checkpoint is reached only after validation evidence is captured

### Task Dependency Graph

```text
T001 -> T002
T001 -> T003
T001 -> T004
T002,T003,T004 -> T005 -> T006 -> T007 -> T008
T008 -> T009 -> T010,T011,T012,T013,T014,T015 -> T016 -> T017
T017 -> T018 -> T019 -> T020,T021 -> T022 -> T023
T017 -> T024 -> T025,T026 -> T027 -> T028 -> T029
T023,T029 -> T030,T031,T032 -> T033 -> T034
```

---

## Parallel Execution Examples

### User Story 1 (US1)

```text
Run in parallel after T009:
- T010 (Contract file set A)
- T011 (Contract file set B)
- T012 (Integration file set A)
- T013 (Integration file set B)
- T014 (Unit file set A)
- T015 (Unit file set B)
```

### User Story 2 (US2)

```text
Run in parallel after T019:
- T020 generate non-exact JSON artifact
- T021 generate non-exact Markdown artifact
```

### User Story 3 (US3)

```text
Run in parallel after T024:
- T025 namespace/attribute usage audit
- T026 dependency decision matrix generation
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2
2. Complete Phase 3 (US1)
3. Validate with `specs/00014-xunit-assertivo-migration/artifacts/us1-validation.md` and `specs/00014-xunit-assertivo-migration/artifacts/semantic-equivalence-sample.md`
4. Stop and review before proceeding to US2/US3

### Incremental Delivery

1. Setup + Foundational automation
2. Deliver US1 conversion and staged validation
3. Deliver US2 non-exact artifact workflow and consistency checks
4. Deliver US3 scope/dependency governance checks
5. Execute Polish phase and full completion gate

### Parallel Team Strategy

1. Team jointly completes Phase 1 and Phase 2
2. After US1 conversion checkpoint:
   - Engineer A: US2 artifact generation and consistency checks
   - Engineer B: US3 scope and dependency governance
3. Merge for Phase 6 final validation and release-readiness evidence

---

## Notes

- [P] tasks are safe parallel opportunities due separate files or independent outputs
- Story labels ensure traceability from tasks to spec user stories
- Keep unsupported and non-exact assertions unchanged by design
- Do not modify production code under `src/` for this feature
- xUnit dependencies remain by default unless explicit removal criteria are satisfied
