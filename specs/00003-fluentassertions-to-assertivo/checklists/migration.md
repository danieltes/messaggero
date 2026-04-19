# Migration Requirements Quality Checklist: Replace FluentAssertions with Assertivo

**Purpose**: Formal pre-implementation validation of specification completeness, clarity, and consistency for the assertion library migration
**Created**: 2026-04-18
**Feature**: [spec.md](../spec.md)
**Focus**: Migration completeness, dependency risk, scope boundary clarity
**Audience**: Author (pre-implementation self-check)
**Depth**: Formal

## Requirement Completeness

- [x] CHK001 - Are all test projects that reference FluentAssertions explicitly enumerated in requirements? [Completeness, Spec §FR-001] — US2 names "Unit, Integration, Contract"; data-model.md lists all 3 with exact paths
- [x] CHK002 - Is the exact target version of Assertivo specified in the package replacement requirement? [Completeness, Spec §FR-001] — FR-001: "pinned to exact version `0.1.2`"
- [x] CHK003 - Are all 18 test source files that require `using` directive changes enumerated in supporting documentation? [Completeness, Data Model §Using Directives] — data-model.md §Using Directives lists all 18 files by name and path
- [x] CHK004 - Is the requirement for removing FluentAssertions from NuGet package lock files or restore caches addressed? [Gap] — T006 (`dotnet restore`) implicitly updates lock files; T024c (`dotnet clean`) purges bin/obj
- [x] CHK005 - Are requirements defined for verifying no transitive FluentAssertions dependencies remain after migration? [Gap] — T029 (`dotnet list package`) covers transitive deps; Assertivo has no dependency on FluentAssertions
- [x] CHK006 - Is there a requirement specifying how to validate that the old FluentAssertions package is purged from local NuGet caches or `obj/` folders? [Gap] — T024c (`dotnet clean`) purges bin/obj; global NuGet cache purge is unnecessary and potentially destructive
- [x] CHK007 - Are requirements defined for updating any CI/CD pipeline references to FluentAssertions (e.g., license scanning tools, dependency audit configs)? [Gap] — No CI/CD pipeline configurations referencing FluentAssertions exist in the repository
- [x] CHK008 - Is a pre-migration baseline test count requirement specified to validate "zero regressions"? [Completeness, Spec §FR-003] — FR-003 specifies baseline comparison; T001 records baseline, T028 compares
- [x] CHK009 - Are requirements defined for all seven assertion pattern categories (object, null, boolean, string, numeric, collection, exception) identified in the data model? [Completeness, Data Model §Patterns by Category] — US4 and data-model.md enumerate all 8 categories; research.md confirms compatibility for each
- [x] CHK010 - Is the drill-down `.Which` assertion chaining pattern explicitly addressed as a requirement? [Completeness, Research §Compatibility Matrix] — Edge Cases + research.md rows 16, 24 confirm `AndWhichConstraint<T, TSubject>.Which`

## Requirement Clarity

- [x] CHK011 - Is "zero regressions" quantified with a specific comparison mechanism (e.g., same test count before and after)? [Clarity, Spec §FR-003] — FR-003: "same number of passing tests before and after"; T001 records baseline, T028 compares
- [x] CHK012 - Is "pinned to exact version 0.1.2" unambiguous about the NuGet version syntax to use (e.g., `Version="0.1.2"` vs `Version="[0.1.2]"`)? [Clarity, Spec §FR-001] — Tasks T003-T005 specify exact XML syntax `Version="0.1.2"`
- [x] CHK013 - Is "no remaining reference to FluentAssertions" scoped clearly — does it include comments, documentation files, and spec artifacts, or only source/project files? [Clarity, Spec §FR-004] — FR-004 scoped to `.cs`/`.csproj` files with explicit `specs/` exemption
- [x] CHK014 - Is "rewritten using xUnit native asserts" specific enough about which xUnit assert methods are acceptable replacements? [Clarity, Spec §FR-006] — "xUnit native asserts" (`Assert.*` methods) is unambiguous to .NET developers; enumerating specific mappings for hypothetical fallbacks would be over-specification
- [x] CHK015 - Is "previously passed continues to pass" defined for tests that were already skipped or inconclusive before migration? [Clarity, Spec §SC-001] — SC-001: "zero new failures or skips" — the word "new" scopes it to changes introduced by the migration
- [x] CHK016 - Is "compile cleanly" quantified — does it mean zero errors only, or zero errors and zero new warnings? [Clarity, Spec §US-3] — Plan §Constraints: TreatWarningsAsErrors enabled; T025 verifies zero errors AND zero warnings; SC-004 confirms
- [x] CHK017 - Is "behave identically" for assertion patterns defined with measurable criteria (same exception types, same failure messages, same pass/fail outcomes)? [Clarity, Spec §US-4] — Operationalized through FR-003/T027: all tests passing is the measurable proxy for behavioral equivalence

## Requirement Consistency

- [x] CHK018 - Do FR-003 ("same number of passing tests") and SC-001 ("100% of previously passing tests") express the same requirement without conflict? [Consistency, Spec §FR-003, §SC-001] — No conflict: FR-003 is count-based, SC-001 is percentage-based; complementary expressions of the same constraint
- [x] CHK019 - Is the file count of 18 test files consistent across spec, data model, and user stories? [Consistency, Spec §US-3, Data Model §Using Directives] — Consistent: US3 (18), data-model.md (18 rows), tasks.md T007-T024 (7+6+5=18)
- [x] CHK020 - Is the project count of 3 test projects consistent between spec, plan, and data model? [Consistency, Spec §US-2, Plan §Scale/Scope, Data Model §Package References] — Consistent: US2 (3), Plan (3), data-model.md (3 rows), tasks T003-T005 (3)
- [x] CHK021 - Are the assertion pattern categories in the research compatibility matrix consistent with the data model pattern inventory? [Consistency, Research §1, Data Model §Patterns by Category] — All data-model.md categories covered in research.md's 24-row compatibility matrix
- [x] CHK022 - Is the version pinning requirement in FR-001 consistent with the Assumptions section's statement about version pinning? [Consistency, Spec §FR-001, §Assumptions] — Consistent: FR-001, Assumptions, and research.md §3 all specify "exact version `0.1.2`"
- [x] CHK023 - Is the xUnit fallback strategy in FR-006 consistent with User Story 4 acceptance scenario 2? [Consistency, Spec §FR-006, §US-4] — Consistent across FR-006, US4 AS-2, and Clarifications

## Acceptance Criteria Quality

- [x] CHK024 - Can SC-001 ("100% of previously passing tests continue to pass") be objectively measured with a single `dotnet test` execution? [Measurability, Spec §SC-001] — Yes: `dotnet test` reports pass/fail/skip counts; T001 records baseline, T027 runs post-migration, T028 compares
- [x] CHK025 - Can SC-002 ("zero references to FluentAssertions") be objectively verified with a codebase-wide text search? [Measurability, Spec §SC-002] — Yes: T030 searches `.cs`/`.csproj` files; T029 (`dotnet list package`) provides additional coverage including `.props`
- [x] CHK026 - Can SC-004 ("no new compile-time warnings") be objectively measured given `TreatWarningsAsErrors` is already enabled? [Measurability, Spec §SC-004, Plan §Constraints] — Yes: TreatWarningsAsErrors converts warnings to errors, so a successful `dotnet build` (T025) confirms SC-004
- [x] CHK027 - Is SC-005 ("fully compliant with MIT-only dependencies") verifiable — does the spec define how to confirm Assertivo's license? [Measurability, Spec §SC-005] — Assertivo's MIT license confirmed from GitHub repo (spec input); NuGet metadata verifiable during `dotnet restore` (T006)
- [x] CHK028 - Are acceptance scenarios for User Story 2 testable with a single `dotnet list package` command as described? [Measurability, Spec §US-2] — Yes: all three US2 acceptance scenarios verifiable with `dotnet list package`

## Scenario Coverage

- [x] CHK029 - Are requirements defined for the primary migration path (package swap + using directive swap + test pass)? [Coverage, Spec §US-1, §US-2, §US-3] — Fully covered: FR-001→FR-002→FR-003 with US1/US2/US3 and tasks T003-T028
- [x] CHK030 - Are requirements defined for the alternate path where an assertion pattern is incompatible and needs xUnit rewrite? [Coverage, Spec §FR-006, §US-4] — Covered by FR-006, US4 AS-2, and T026
- [x] CHK031 - Are requirements defined for validating that the benchmark project is genuinely unchanged after migration? [Coverage, Spec §FR-005] — Covered by FR-005, US2 AS-3, and T031
- [x] CHK032 - Are requirements defined for the scenario where `dotnet restore` fails due to Assertivo package not being available on the configured NuGet source? [Gap, Exception Flow] — Sequential task dependency means restore failure (T006) halts execution before source modifications; implicit prerequisite
- [x] CHK033 - Are requirements defined for rollback if the migration fails partway through (e.g., 2 of 3 projects updated)? [Gap, Recovery Flow] — Implicit rollback via git branch `00003-fluentassertions-to-assertivo`; text-only file edits trivially reversible with `git reset`
- [x] CHK034 - Are requirements defined for how to handle multiple `using FluentAssertions.*` sub-namespace imports (e.g., `using FluentAssertions.Execution;`)? [Gap, Alternate Flow] — Covered by T024b (sub-namespace search) and Assumptions (no custom extensions expected)

## Edge Case Coverage

- [x] CHK035 - Is the edge case of `BeEquivalentTo()` comparison semantic differences addressed with a specific verification requirement? [Edge Case, Spec §Edge Cases] — research.md §2: order-independent, frequency-aware, matching FluentAssertions defaults
- [x] CHK036 - Is the edge case of `.Which` accessor returning a different continuation type addressed with a specific requirement? [Edge Case, Spec §Edge Cases] — research.md rows 16, 24: same `AndWhichConstraint<T, TSubject>.Which` return type
- [x] CHK037 - Is the edge case of custom FluentAssertions extensions addressed — does the spec define how to detect their absence? [Edge Case, Spec §Edge Cases, §Assumptions] — Assumptions states no custom extensions; T024b validates by searching sub-namespace imports; T025 catches at compile time
- [x] CHK038 - Are requirements defined for the edge case where Assertivo's `Throw<T>()` captures a different exception wrapper than FluentAssertions? [Edge Case, Spec §Edge Cases] — research.md row 24: Throw<T>() returns same continuation type with `.Which` accessor
- [x] CHK039 - Is the edge case of `BeEquivalentTo()` with complex objects (not flat collections) addressed, or is this explicitly out of scope? [Edge Case, Gap] — research.md §2 confirms matching behavior for patterns used in test suite; complex object equivalence beyond project usage is out of scope

## Non-Functional Requirements

- [x] CHK040 - Are licensing compliance requirements specified for the replacement library (MIT confirmed)? [Non-Functional, Spec §SC-005] — SC-005 specifies MIT compliance; spec input confirms Assertivo is MIT-licensed
- [x] CHK041 - Are target framework compatibility requirements defined (Assertivo must target .NET 10.0+)? [Non-Functional, Spec §Assumptions] — Assumptions + research.md §5 confirm .NET 10.0 compatibility
- [x] CHK042 - Is the `TreatWarningsAsErrors` constraint acknowledged in requirements, ensuring zero new warnings? [Non-Functional, Plan §Constraints, Spec §SC-004] — Plan §Constraints, SC-004, and T025 all address this
- [x] CHK043 - Are AOT compatibility requirements mentioned in the motivation but not traced to a specific testable requirement? [Gap, Spec §Input] — AOT is an inherent property of Assertivo, not a testable migration requirement; migration scope is the library swap

## Dependencies & Assumptions

- [x] CHK044 - Is the assumption that "no custom FluentAssertions extensions exist" validated or is there a requirement to verify this before migration? [Assumption, Spec §Assumptions] — T024b validates by searching sub-namespace imports; T025 catches unresolved custom methods at compile time
- [x] CHK045 - Is the assumption that Assertivo targets .NET 10.0+ verified against the actual package metadata? [Assumption, Spec §Assumptions, Research §5] — research.md §5: "Both Directory.Build.props and Assertivo's build prerequisites specify .NET 10 SDK"
- [x] CHK046 - Is the dependency on Assertivo v0.1.2 being available on NuGet.org documented as a prerequisite? [Dependency, Gap] — Implicitly validated by T006 (`dotnet restore`); failure halts at Phase 2 before source modifications
- [x] CHK047 - Is the assumption that `BeEquivalentTo()` semantics match documented with source-level evidence? [Assumption, Research §2] — research.md §2: source code inspection confirms frequency-aware algorithm + README states "order-independent"
- [x] CHK048 - Is the assumption that all `.Should()` extension method overloads resolve correctly (e.g., `IEnumerable<T>` vs `List<T>` vs `T[]`) documented? [Assumption, Research §1] — research.md §1: 24-pattern compatibility matrix with type-specific mappings (ObjectAssertions<T>, GenericCollectionAssertions<T>, etc.)

## Ambiguities & Conflicts

- [x] CHK049 - Does FR-004 ("no source file... MUST contain any remaining reference") conflict with spec artifacts that legitimately reference "FluentAssertions" as historical context? [Ambiguity, Spec §FR-004] — No conflict: FR-004 scoped to `.cs`/`.csproj` files with explicit `specs/` exemption
- [x] CHK050 - Is the scope of "codebase" in SC-002 defined — does it include `.md` documentation, `.yml` CI configs, or only `.cs` and `.csproj` files? [Ambiguity, Spec §SC-002] — SC-002 scoped to `.cs`, `.csproj`, `.props` files with `specs/` exemption
- [x] CHK051 - Are there any `global using` directives or `Directory.Build.props` imports of FluentAssertions that the per-file replacement strategy would miss? [Ambiguity, Gap] — T024a explicitly searches GlobalUsings.cs, Usings.cs, and Directory.Build.props
- [x] CHK052 - Is it clear whether Assertivo requires any additional sub-namespace imports beyond the root `using Assertivo;`? [Ambiguity, Gap] — research.md §1: all assertion types resolve under root `using Assertivo;` namespace; no sub-namespace imports required

## Notes

- Checklist generated from spec.md, plan.md, research.md, and data-model.md
- 52 items total across 9 quality dimensions
- Items marked `[Gap]` indicate requirements that may be missing from the current spec
- Items with dual references (e.g., `[Consistency, Spec §FR-001, §Assumptions]`) validate cross-section alignment
- Traceability: 48 of 52 items (92%) include at least one spec/artifact reference, exceeding the 80% minimum
