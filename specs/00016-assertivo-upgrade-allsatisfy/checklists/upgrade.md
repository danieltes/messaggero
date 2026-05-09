# Upgrade Release Gate Checklist: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Purpose**: Thorough release gate — validate requirements quality for the Assertivo version bump (User Story 1, FR-001–FR-003, SC-001–SC-002) before sign-off
**Created**: 2026-05-09
**Feature**: [spec.md](../spec.md) | **Audience**: PR reviewer

---

## Requirement Completeness

- [x] CHK001 - Are all three test project `.csproj` files explicitly named in FR-001 (Unit, Contract, Integration)? [Completeness, Spec §FR-001]
  > PASS — FR-001 explicitly names `Messaggero.Tests.Unit.csproj`, `Messaggero.Tests.Contract.csproj`, and `Messaggero.Tests.Integration.csproj`.
- [x] CHK002 - Is the exact target version (`0.3.0`) specified in every version-bump requirement, leaving no ambiguity about the destination? [Completeness, Spec §FR-001]
  > PASS — FR-001 states "updated from `0.2.0` to `0.3.0`"; SC-001 references `0.3.0` directly.
- [x] CHK003 - Is the source version (`0.2.0`) documented in FR-001 to establish the precise upgrade delta and enable before/after verification? [Completeness, Spec §FR-001]
  > PASS — FR-001 states "from `0.2.0`", establishing the before-state for before/after verification.
- [x] CHK004 - Are build-success criteria (SC-002) defined independently of test-run criteria (SC-003) so the version bump can be verified in isolation before the conversion step? [Completeness, Spec §SC-002]
  > PASS — SC-002 covers build (exit code + warning count); SC-003 covers test green. US1 "Independent Test" confirms the version bump can be verified in isolation before conversion.
- [x] CHK005 - Is there a requirement explicitly prohibiting changes to `xunit.v3` and `xunit.runner.visualstudio` versions as a co-located constraint to the Assertivo bump? [Completeness, Spec §FR-008]
  > PASS — FR-008 explicitly names both `xunit.v3` and `xunit.runner.visualstudio` as prohibited from change.

## Requirement Clarity

- [x] CHK006 - Is "zero warnings" in FR-002 and SC-002 unambiguous — does the spec clarify whether warnings suppressed via `NoWarn` (e.g., the existing `xUnit1051` suppression) count against this criterion? [Clarity, Spec §FR-002]
  > PASS — FR-002 explicitly states "zero new warnings" and calls out that pre-existing `NoWarn` suppressions (e.g., `xUnit1051`) do not count against this criterion.
- [x] CHK007 - Is "exit code 0" in FR-002 specific about the build invocation scope — solution-level `dotnet build Messaggero.slnx` versus per-project builds? [Clarity, Spec §FR-002]
  > PASS — FR-002 says "the solution MUST build", clearly indicating solution-level scope; US1 Independent Test and quickstart.md both specify `dotnet build Messaggero.slnx`.
- [x] CHK008 - Is FR-003 ("all tests that passed before MUST continue to pass") precise about what constitutes the baseline — is a prior green test run referenced or implied by the 00014 migration outcome? [Clarity, Spec §FR-003]
  > PASS — FR-003 explicitly pins the baseline to `specs/00014-xunit-assertivo-migration/artifacts/final-validation.md`.
- [x] CHK009 - Is the rationale for upgrading to `0.3.0` (to gain `AllSatisfy`) stated in the spec so reviewers can confirm this is a targeted upgrade, not a blanket "latest version" policy? [Clarity, Spec §US1]
  > PASS — US1 "Why this priority" explicitly states the upgrade is required to gain access to the new `AllSatisfy` API.

## Acceptance Criteria Quality

- [x] CHK010 - Is SC-001 objectively verifiable by a static scan of `.csproj` files for the version string `0.3.0` — no subjective interpretation required? [Measurability, Spec §SC-001]
  > PASS — SC-001 is verifiable by grepping the three `.csproj` files for `0.3.0`; binary result, no subjectivity.
- [x] CHK011 - Is SC-002 objectively verifiable by inspecting `dotnet build` exit code and warning count — no subjective interpretation required? [Measurability, Spec §SC-002]
  > PASS — exit code and warning count are both machine-readable outputs from `dotnet build`; no subjectivity.
- [x] CHK012 - Do US1 acceptance scenarios express outcomes in observable terms (exit code, warning count) rather than internal mechanisms (NuGet restore internals, MSBuild targets)? [Acceptance Criteria, Spec §US1]
  > PASS — US1 scenario 1 uses "exit code 0 and zero warnings"; scenario 2 uses "no previously passing tests regress" — both are externally observable.

## Scenario Coverage

- [x] CHK013 - Does the spec define the expected outcome if only a subset of the three `.csproj` files is updated (partial upgrade scenario)? [Coverage, Edge Case, Gap]
  > INTENTIONAL GAP — tasks T001, T002, and T003 are executed as a single phase with no merge until all three are updated; a partial upgrade is not a valid delivery state in this workflow. Spec-level coverage of this failure mode adds no reviewer value.
- [x] CHK014 - Is the NuGet package restore step addressed — is it explicit that `dotnet build` implicitly restores, or is a separate restore gate required? [Coverage, Assumption, Spec §Assumptions]
  > INTENTIONAL GAP — `dotnet build` performs implicit NuGet restore per .NET SDK standard behavior; no explicit restore step is required or useful to specify. Assumption that `Assertivo 0.3.0` is available on NuGet is documented.
- [x] CHK015 - Is the regression coverage in FR-003 scoped to "all test projects" (Unit, Contract, Integration) or only those affected by the version bump? [Coverage, Spec §FR-003]
  > PASS — FR-003 says "All tests that passed at HEAD of main" — scoped to all test projects, not just those directly affected.

## Edge Cases

- [x] CHK016 - Does the spec address what happens if `Assertivo 0.3.0` introduces a breaking change to an existing `Should()` chain used in the test suite? [Edge Case, Spec §Edge Cases]
  > PASS — Edge Cases explicitly states "If `0.3.0` introduces any breaking changes to APIs used in existing `Should()` chains, those must be resolved before the `Assert.All` conversion proceeds."
- [x] CHK017 - Are transitive dependency implications of the Assertivo 0.3.0 bump documented — e.g., does 0.3.0 introduce new transitive package dependencies that could affect build or lock-file state? [Edge Case, Gap]
  > INTENTIONAL GAP — research.md RQ-2 confirmed no new transitive dependencies introduced by 0.3.0; the build gate (SC-002) will surface any lock-file or resolution issues immediately. No spec-level note required.

## Dependencies & Assumptions

- [x] CHK018 - Is the assumption that `Assertivo 0.3.0` is available on NuGet at implementation time explicitly documented in the spec? [Assumption, Spec §Assumptions]
  > PASS — Assumptions: "`Assertivo 0.3.0` is available on NuGet at the time of implementation."
- [x] CHK019 - Is the assumption that all three projects currently reference exactly `0.2.0` (not a mixed version state) documented, enabling a clean single-version upgrade? [Assumption, Spec §Assumptions]
  > PASS — FR-001 explicitly states "from `0.2.0`", establishing the expected current state; data-model.md change surface table confirms all three list `0.2.0` → `0.3.0`.

## Notes

- Items marked `[Gap]` indicate requirements not explicitly present in the current spec — evaluate whether they are intentionally omitted or need to be added before implementation.
- Check items off as completed: `[x]`
- This checklist covers **User Story 1 only** (version bump). See `migration.md` for the assertion-conversion gate.
