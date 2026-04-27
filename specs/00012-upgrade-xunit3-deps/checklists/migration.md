# Specification Quality Checklist: xUnit 3 Migration & Dependency Upgrade

**Purpose**: Validate requirement completeness, clarity, consistency, and coverage for the xUnit 3 migration and dependency upgrade specification
**Created**: 2026-04-26
**Reviewed**: 2026-04-26
**Feature**: [spec.md](../spec.md)
**Focus**: Migration Completeness + Dependency Upgrade Coverage (combined)
**Audience**: Author (pre-implementation self-review) + Reviewer (PR gate)
**Depth**: Thorough
**Status**: ✅ ALL ITEMS PASSED — ready for `/speckit.implement`

---

## Requirement Completeness

- [x] CHK001 - Is the mandatory `<OutputType>Exe</OutputType>` project-file change for xUnit v3 explicitly required in the functional requirements? [Gap — not mentioned in FR-001 or FR-014; only discovered in research.md §2]
- [x] CHK002 - Are all four `Microsoft.Extensions.*` packages in `Messaggero.csproj` individually named (DependencyInjection.Abstractions, Logging.Abstractions, Options, Hosting.Abstractions), or is the wildcard `Microsoft.Extensions.*` sufficient to prevent implementer ambiguity? [Completeness, FR-005]
- [x] CHK003 - Is the xUnit v3 package rename (`xunit` → `xunit.v3`) explicitly stated as a requirement, or is it left implicit under "reference xUnit v3"? [Completeness, Gap, FR-001]
- [x] CHK004 - Is there a requirement explicitly covering the removal of the `xunit` 2.x package reference (not just addition of `xunit.v3`)? [Completeness, Gap, FR-001]
- [x] CHK005 - Is the `Messaggero.Testing` source project's status (no package references; no changes required) explicitly documented in the spec, or only in research.md? [Completeness, Assumption]
- [x] CHK006 - Are acceptance criteria defined for the Benchmarks project specifically (e.g., that it still builds and produces an executable after BenchmarkDotNet upgrade)? [Completeness, FR-010]
- [x] CHK007 - Is there a requirement or assumption documenting that the Benchmarks project already has `<OutputType>Exe</OutputType>` and does not need the xUnit v3 migration treatment? [Completeness, Assumption]
- [x] CHK008 - Are all projects in which `Microsoft.Extensions.DependencyInjection` (non-Abstractions) appears explicitly identified (Unit + Integration; not Contract or source projects)? [Completeness, FR-005]

---

## Requirement Clarity

- [x] CHK009 - Does FR-001 ("reference xUnit v3 (3.x latest stable)") unambiguously convey that the NuGet package ID changes from `xunit` to `xunit.v3`, or is it ambiguous enough to allow an implementer to simply bump `xunit` to a non-existent 3.x version? [Clarity, Ambiguity, FR-001]
- [x] CHK010 - Is "compatible with xUnit 3" in FR-006 defined with enough precision to allow verification, or does it require the implementer to independently determine compatibility? [Clarity, FR-006]
- [x] CHK011 - Is "latest stable release" in FR-007 through FR-015 sufficiently defined by the Assumptions section, or does the definition need to be cross-referenced more explicitly in each FR? [Clarity, Assumption]
- [x] CHK012 - Does FR-008 ("NSubstitute MUST be updated to the latest stable release") accurately reflect the expected outcome when NSubstitute is already at latest stable — i.e., is "updated to" potentially misleading when no change is required? [Clarity, Conflict, FR-008]
- [x] CHK013 - Is "zero warnings" in SC-001 defined precisely enough — e.g., does it exclude suppressed warnings, info-level diagnostics, or warnings from transitive dependencies? [Clarity, SC-001]
- [x] CHK014 - Is the "pre-migration baseline" referenced in SC-002 formally defined or documented, or is it implicitly assumed to be "current test results"? [Clarity, SC-002]
- [x] CHK015 - Is SC-003 ("zero package references to `xunit` 2.x") scoped to direct references only, or does it include transitive/indirect package references pulled in by other packages? [Clarity, SC-003]

---

## Requirement Consistency

- [x] CHK016 - Does FR-005 ("updated to `10.0.0` stable") conflict with SC-005 ("every package at its latest stable release") given that research.md §7 establishes the actual latest stable is `10.0.7`, not `10.0.0`? [Conflict, FR-005 vs SC-005]
- [x] CHK017 - Does User Story 2 Acceptance Scenario 1 ("updated to `10.0.0` stable") align with the research-established target of `10.0.7`, and if not, does this create implementer confusion? [Conflict, User Story 2 vs research.md §7]
- [x] CHK018 - Does the Assumption "Messaggero.Testing source project… requires only the `Microsoft.Extensions.*` update" contradict data-model.md which states "No package references. No changes required"? [Conflict, Assumption vs data-model.md]
- [x] CHK019 - Does FR-014 ("breaking changes MUST be resolved, including `IAsyncLifetime` namespace changes") conflict with the research finding (§3) that there are zero `IAsyncLifetime` usages in this codebase — i.e., is the FR enumerating breaking changes that don't apply? [Conflict, FR-014 vs research.md §3]
- [x] CHK020 - Does FR-013 ("Directory.Build.props MUST be updated for any package version properties it already centralises") align with data-model.md's conclusion that no changes are required because no versions are centralised there? [Consistency, FR-013 vs data-model.md]
- [x] CHK021 - Is the constraint "all upgrades MUST be delivered as a single atomic commit" (Assumptions) consistent with the two independently testable User Stories that imply distinct logical phases? [Consistency, Assumption vs User Story independence]
- [x] CHK022 - Does FR-008 say NSubstitute "MUST be updated" while research.md §9 says "no change" — and if so, is the spec consistent with the research-established outcome? [Conflict, FR-008 vs research.md §9]

---

## Acceptance Criteria Quality

- [x] CHK023 - Are SC-001 through SC-005 each independently verifiable without requiring human judgment — i.e., can each criterion be evaluated with a deterministic check (grep, build exit code, test result count)? [Measurability]
- [x] CHK024 - Is there a success criterion specifically covering the Benchmarks project outcome (e.g., `dotnet build` succeeds on that project)? [Coverage, Gap, SC-001 scope]
- [x] CHK025 - Does SC-005 ("every package… at its latest stable release at the time the migration is completed") adequately handle the case where NSubstitute's "latest stable" is the same as the current version — i.e., is "no change needed" a valid way to satisfy "at latest stable"? [Measurability, SC-005]
- [x] CHK026 - Are the success criteria ordered by priority or dependency (e.g., SC-001 build must pass before SC-002 tests can run), and is this ordering documented? [Clarity, Acceptance Criteria]

---

## Scenario Coverage

- [x] CHK027 - Is there a requirement or edge case addressing what happens if `dotnet restore` fails after the package version changes (e.g., a package version is unavailable or feed is unreachable)? [Coverage, Gap, Exception Flow]
- [x] CHK028 - Is there a requirement addressing what happens if `Assertivo` 0.2.0 introduces assertion API changes that break existing test assertions? [Coverage, Gap, Edge Case]
- [x] CHK029 - Is the upgrade validation flow for the Benchmarks project defined — specifically, does the spec require that benchmarks actually *run* (not just compile), or is build-only validation sufficient? [Coverage, FR-010]
- [x] CHK030 - Are requirements defined for the scenario where xUnit v3's changed parallelism defaults cause previously passing tests to become flaky (as opposed to outright failing)? [Coverage, Edge Case, FR-014]

---

## Edge Case Coverage

- [x] CHK031 - Edge case 4 in the spec describes `IAsyncLifetime` requiring namespace migration, but research confirms zero usages exist. Is retaining this edge case in the spec misleading, or is it documented as a "check performed, no action needed" finding? [Clarity, Conflict, Edge Case vs research.md §3]
- [x] CHK032 - Is there an edge case or requirement addressing version drift — i.e., what the implementer should do if a "latest stable" version changes between the spec being written (2026-04-26) and implementation? [Edge Case, Gap]
- [x] CHK033 - Is there an edge case covering transitive dependency conflicts that could arise when multiple packages (e.g., Testcontainers + RabbitMQ.Client) are upgraded simultaneously? [Edge Case, Gap]

---

## Non-Functional Requirements

- [x] CHK034 - Is there a security requirement to verify that the upgraded packages do not introduce known CVEs or security advisories (e.g., via `dotnet list package --vulnerable`)? [Gap, Non-Functional]
- [x] CHK035 - Is there a reproducibility requirement — e.g., that package versions are pinned (exact version strings, not floating ranges) — to ensure the build is deterministic after the upgrade? [Non-Functional, Coverage]

---

## Notes

**Review session**: 2026-04-26 — reviewed by author in conjunction with `speckit.analyze` output.

**Items resolved by spec/tasks amendments** (via `speckit.analyze` remediation before this review):
- **CHK001/CHK003/CHK004/CHK009**: FR-001 and FR-014 updated to name `xunit.v3` package ID and `<OutputType>Exe</OutputType>` requirement explicitly.
- **CHK012/CHK022**: FR-008 amended to clarify NSubstitute 5.3.0 is already at latest stable; no version change required.
- **CHK013**: SC-001 scoped to MSBuild warnings; transitive NuGet deprecation notices excluded.
- **CHK016/CHK017**: FR-005 and US2 Acceptance Scenario 1 corrected to `10.0.7` (research-established target).
- **CHK018/CHK005**: Assumptions corrected — `Messaggero.Testing` has no NuGet package references and requires no changes.
- **CHK020**: FR-013 amended to document the researched outcome (no centralised versions; no changes needed).

**Items resolved by tasks amendments**:
- **CHK006/CHK024/CHK029**: T020 (benchmark dry-run, `--job Dry`) added to Phase 4; T013 explicitly scoped to include Benchmarks project.
- **CHK034**: T021 (`dotnet list package --vulnerable`) added to Phase 5 as a parallel validation gate.

**Items accepted as-is** (no action required):
- **CHK002**: FR-005 wildcard `Microsoft.Extensions.*` is sufficient; all four individual packages are enumerated in tasks T002/T004/T008.
- **CHK007**: Benchmarks project `<OutputType>Exe</OutputType>` confirmed present in data-model.md; Assumptions scope xUnit migration to test projects only.
- **CHK008**: ME.DI (non-Abstractions) distribution documented in tasks: T002 (Unit), T004 (Integration); T003 (Contract) omits correctly.
- **CHK010**: research.md §10 pins exact compatible runner version (3.1.5); tasks T002–T004 specify it.
- **CHK011**: Assumptions section defines "latest stable" authoritatively.
- **CHK014**: T001 establishes formal baseline ("record baseline test pass counts").
- **CHK015**: SC-003 targets direct `.csproj` references; T016 greps project files only.
- **CHK019**: FR-014 mandates the check be performed; research §3 documents the outcome (0 `IAsyncLifetime` usages, no action needed). Retaining the requirement is correct — it mandates verification, not the presence of a pattern.
- **CHK021**: Consistent — stories are developed and tested independently, then committed together in one atomic commit per delivery constraint.
- **CHK023**: All SCs are deterministic: exit code (SC-001), test pass count (SC-002), grep (SC-003/SC-004), `dotnet list package` (SC-005).
- **CHK025**: SC-005 is satisfied by a package being at latest stable regardless of whether a version bump was performed. NSubstitute 5.3.0 = latest stable = SC-005 satisfied.
- **CHK026**: Task dependency graph in tasks.md documents T018 (SC-001 build gate) must pass before T019 (SC-002 test gate).
- **CHK027**: Restore failures are operational/infrastructure concerns outside the migration scope; the validation chain (T005/T012) detects them immediately.
- **CHK028**: Assertivo API breaking changes are caught by the test validation gates (T007/T014/T019) before commit.
- **CHK030**: research.md §5 confirms 0 `CollectionBehavior` or parallelism attributes in codebase; this scenario cannot occur.
- **CHK031**: Edge case 4 is correctly retained — it mandates the check; research §3 documents the result (nothing to do). Not misleading.
- **CHK032**: Assumptions pin "latest stable at time of migration"; research.md §10 provides exact pinned versions for implementation.
- **CHK033**: Transitive conflicts are caught by the restore/build chain (T005/T012 restore, T006/T013 build).
- **CHK035**: All package references across every `.csproj` use exact version strings (e.g., `Version="3.2.2"`); no floating ranges present.
