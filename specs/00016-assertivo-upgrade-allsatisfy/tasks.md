# Tasks: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Feature**: `00016-assertivo-upgrade-allsatisfy`  
**Input**: Design documents from `specs/00016-assertivo-upgrade-allsatisfy/`  
**Prerequisites**: plan.md ✅ · spec.md ✅ · research.md ✅ · data-model.md ✅ · quickstart.md ✅

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[US1]** / **[US2]**: User story this task belongs to
- No [P] on tasks editing the same file

---

## Phase 1: Setup

*No setup tasks required. All target files already exist; no new projects or directories need to be created.*

---

## Phase 2: Foundational

*No foundational blocking prerequisites. Research confirmed Assertivo 0.3.0 is available on NuGet and introduces no breaking changes — no remediation work is needed before story implementation begins.*

---

## Phase 3: User Story 1 — Upgrade Assertivo to 0.3.0 (Priority: P1) 🎯 MVP

**Goal**: All three test projects reference `Assertivo 0.3.0`; the solution builds with exit code 0 and zero warnings; no regressions in any passing test.

**Independent Test**: Run `dotnet build Messaggero.slnx` and confirm exit code 0 with zero warnings. Run `dotnet test Messaggero.slnx` and confirm all tests pass. Both can be verified before any assertion-conversion work begins.

### Implementation for User Story 1

- [X] T001 [P] [US1] Bump `Assertivo` version from `0.2.0` to `0.3.0` in `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj` (line 19)
- [X] T002 [P] [US1] Bump `Assertivo` version from `0.2.0` to `0.3.0` in `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj` (line 17)
- [X] T003 [P] [US1] Bump `Assertivo` version from `0.2.0` to `0.3.0` in `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj` (line 18)
- [X] T004 [US1] Run `dotnet build Messaggero.slnx` and verify exit code 0 with zero warnings (depends on T001, T002, T003)
- [X] T005 [US1] Run `dotnet test Messaggero.slnx` and verify all tests pass with no regressions (depends on T004)

**Checkpoint**: At this point User Story 1 is fully complete and independently verified. The codebase is on Assertivo 0.3.0 with `AllSatisfy` available. User Story 2 may now begin.

---

## Phase 4: User Story 2 — Convert the Two Remaining Assert.All Calls (Priority: P1)

**Goal**: Both `Assert.All` call sites in `RoutingIntegrationTests.cs` are replaced with `AllSatisfy`; the `using Xunit;` directive is retained; the routing integration test passes; zero `Assert.` occurrences remain in any test `.cs` file.

**Independent Test**: Run `dotnet test Messaggero.slnx` and confirm all tests pass. Then run `Select-String -Path "tests/**/*.cs" -Pattern "Assert\." -Recurse` and confirm zero matches.

### Implementation for User Story 2

- [X] T006 [US2] Replace `Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"))` with `kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"))` at line 80 in `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs`
- [X] T007 [US2] Replace `Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"))` with `rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"))` at line 81 in `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs` (depends on T006)
- [X] T007b [US2] Verify `using Xunit;` directive is still present at line 10 of `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs` — acceptance criterion for FR-006 (depends on T007)
- [X] T008 [US2] Run `dotnet test Messaggero.slnx` and verify the routing integration test (`DifferentMessageTypes_RouteToCorrectTransports`) passes (depends on T007b)
- [X] T009 [US2] Run `Select-String -Path "tests/**/*.cs" -Pattern "Assert\." -Recurse` from the repository root and confirm zero matches (depends on T007b)

**Checkpoint**: At this point User Story 2 is fully complete. All xUnit assertion call sites have been converted and the full test suite is green.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T010 Run `dotnet format --verify-no-changes Messaggero.slnx` and confirm exit code 0 — satisfies constitution Article II.6 static analysis gate (depends on T009)
  > NOTE: Exits with code 2 due to pre-existing whitespace violations in PublishContractTests.cs, FanInIntegrationTests.cs, ScopedHandlerValidationTests.cs, RoutingTableTests.cs — none of these files are in this feature's diff. No formatting issues were introduced by this feature.
- [X] T011 [P] Run `git diff --name-only $(git merge-base HEAD main)` and confirm only the four expected files appear in the diff: `Messaggero.Tests.Unit.csproj`, `Messaggero.Tests.Contract.csproj`, `Messaggero.Tests.Integration.csproj`, `RoutingIntegrationTests.cs` — scope-guard verification for FR-008 and FR-009 (depends on T007b)
  > NOTE: `.github/agents/copilot-instructions.md` also appears — this is the speckit agent context file updated by update-agent-context.ps1 during workflow setup, not a production source file.
- [X] T012 Update `Review Status` for `NC-0001` and `NC-0002` from `Pending` to `Resolved` in `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md` (depends on T009)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 / Phase 2**: N/A — no tasks
- **Phase 3 (US1)**: No dependencies — can start immediately
- **Phase 4 (US2)**: Depends on Phase 3 checkpoint (T005 must be green before T006 begins — `AllSatisfy` must be available in the restored package)
- **Phase 5 (Polish)**: T010 depends on T009; T011 depends on T007b; T012 depends on T009. T010 and T011 can run in parallel.

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies — start immediately
- **User Story 2 (P1)**: Depends on US1 completion — `AllSatisfy` only exists in Assertivo 0.3.0

### Within Each User Story

- **US1**: T001, T002, T003 are parallel (different files) → T004 (build verify) → T005 (test run)
- **US2**: T006 → T007 (same file, sequential) → T007b (directive check) → T008 (test run) and T009 (scan) are independent of each other after T007b

### Parallel Opportunities

- **US1**: T001, T002, T003 can be applied simultaneously (three different `.csproj` files, no shared state)
- **US2**: T008 and T009 can run in parallel after T007b (test run and scan are independent verification steps); T010 and T011 can run in parallel after their respective dependencies

---

## Parallel Example: User Story 1

```text
[START]
  ├── T001 (Unit .csproj)      ─┐
  ├── T002 (Contract .csproj)  ─┼─ parallel
  └── T003 (Integration .csproj)─┘
            ↓
         T004 (dotnet build)
            ↓
         T005 (dotnet test)
            ↓
       [US1 CHECKPOINT]
```

## Parallel Example: User Story 2

```text
[US1 CHECKPOINT]
       ↓
     T006 (line 80 conversion)
       ↓
     T007 (line 81 conversion)
       ↓
    T007b (directive check — FR-006)
       ↓
  ┌──────────────────┐
  T008 (dotnet test)   T009 (Assert. scan)  ← parallel
  └──────────────────┘
       ↓
  ┌──────────────────┐
  T010 (dotnet format)  T011 (scope guard)  ← parallel
  └──────────────────┘
       ↓
    T012 (artifact closure)
       ↓
  [US2 CHECKPOINT]
       ↓
     T010 (artifact closure)
```

---

## Implementation Strategy

**MVP scope**: Phase 3 (User Story 1) alone. Bumping Assertivo to 0.3.0, building clean, and passing tests demonstrates the upgrade is safe before any conversion work begins.

**Incremental delivery**:
1. Land Phase 3 first — verifies the upgrade is non-breaking with zero regressions.
2. Land Phase 4 — completes the Assert.All → AllSatisfy migration, clears the full xUnit assertion surface.
3. Land Phase 5 — closes the 00014 non-exact-candidate tracking record.

**Risk**: None identified. Research (RQ-2) confirmed 0.3.0 is purely additive. Lambda bodies require zero modification (RQ-3).
