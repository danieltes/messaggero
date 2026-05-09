# Migration Release Gate Checklist: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Purpose**: Thorough release gate — validate requirements quality for the Assert.All → AllSatisfy conversion (User Story 2, FR-004–FR-009, SC-003–SC-004) before sign-off
**Created**: 2026-05-09
**Feature**: [spec.md](../spec.md) | **Audience**: PR reviewer

---

## Requirement Completeness

- [x] CHK001 - Are both `Assert.All` call sites documented with their exact file path AND line numbers in the Conversion Catalog and FR-004/FR-005? [Completeness, Spec §FR-004, §FR-005, §Conversion Catalog]
  > PASS — File path (`RoutingIntegrationTests.cs`) is in the Conversion Catalog; line numbers (80, 81) are in US2 acceptance scenarios 1 and 2.
- [x] CHK002 - Is the lambda body immutability constraint ("body must not be modified — only the outer call site changes") formally stated in the Functional Requirements, or only in the Edge Cases section? [Completeness, Spec §Edge Cases, Gap]
  > PASS — FR-004 and FR-005 now each contain an explicit "MUST NOT be modified — only the outer call site changes" clause (added during checklist completion).
- [x] CHK003 - Is there a requirement explicitly covering what to do if additional `Assert.` occurrences are discovered during the FR-007 scan beyond the two known call sites? [Completeness, Coverage, Gap]
  > INTENTIONAL GAP — FR-007's zero-match acceptance criterion is itself the gate: any additional `Assert.` occurrence would fail the scan and block merge without requiring a separate discovery-response requirement.
- [x] CHK004 - Is the `using Xunit;` retention requirement (FR-006) backed by an explicit justification (that `[Fact]` attributes depend on it) so that reviewers can evaluate the retention decision independently? [Completeness, Spec §FR-006]
  > PASS — FR-006 states "MUST be retained because xUnit test attributes remain present"; the justification is explicit.

## Requirement Clarity

- [x] CHK005 - Are the before/after transformations in FR-004 and FR-005 specified at exact character level, or could semantically equivalent but differently formatted rewrites be acceptable? [Clarity, Spec §FR-004, §FR-005]
  > PASS — FR-004 and FR-005 contain verbatim code strings; the Conversion Catalog provides the exact character-level before/after; no formatting latitude is implied.
- [x] CHK006 - Is "zero occurrences of `Assert.`" in FR-007 unambiguous about its scope: does it include commented-out assertions, string literals containing `Assert.`, or only live code? [Ambiguity, Spec §FR-007]
  > PASS — FR-007 explicitly states "live (non-comment) source lines" and notes that string literals and comment lines do not constitute a violation.
- [x] CHK007 - Is the scan boundary in FR-007 ("all test `.cs` files") precise — does it include only files under `tests/`, or also `.cs` files anywhere in the repository? [Clarity, Spec §FR-007]
  > PASS — FR-007 explicitly states "under `tests/`" and specifies the exact PowerShell command `Select-String -Path "tests/**/*.cs"`.
- [x] CHK008 - Does SC-004 ("zero occurrences of `Assert.`") align exactly with FR-007 — are the scan pattern and scope consistent between the requirement and the success criterion? [Consistency, Spec §FR-007, §SC-004]
  > PASS — SC-004 references "any test `.cs` file" which aligns with FR-007's `tests/**/*.cs` scope; both use the same `Assert.` pattern.

## Requirement Consistency

- [x] CHK009 - Is FR-006 (`using Xunit;` retained) consistent with FR-007 (zero `Assert.` occurrences) — does the spec clarify that the `Assert` class removed from the call sites and the `Xunit` namespace retained for attributes are from the same `using Xunit;` directive? [Consistency, Spec §FR-006, §FR-007]
  > PASS — Assumptions: "The `using Xunit;` directive covers both xUnit test attributes and was the source of the `Assert.*` namespace; removing `Assert.` calls does not make the directive unused because attributes remain."
- [x] CHK010 - Do FR-004 and FR-005 (exact before/after strings) match the Conversion Catalog verbatim? [Consistency, Spec §FR-004, §FR-005, §Conversion Catalog]
  > PASS — FR-004/FR-005 before/after strings are identical to the Conversion Catalog entries (verified by inspection).
- [x] CHK011 - Is FR-009 ("no production source files modified") consistent with the change surface defined in data-model.md — do both agree on which files are in scope? [Consistency, Spec §FR-009, data-model.md]
  > PASS — FR-009 defines production as files under `src/`; data-model.md change surface lists only files under `tests/` and `specs/`; consistent.

## Acceptance Criteria Quality

- [x] CHK012 - Is SC-003 (`dotnet test` green) independently verifiable from SC-004 (zero `Assert.` scan) — are these two distinct, sequentially checkable gates? [Measurability, Spec §SC-003, §SC-004]
  > PASS — T008 (test run) and T009 (scan) are defined as separate, independently executable tasks in tasks.md; each has a binary pass/fail result.
- [x] CHK013 - Is "identical semantics to before" in US2 acceptance scenario 4 objectively verifiable — is there a defined equivalence criterion between `Assert.All` and `AllSatisfy` in the spec or research? [Measurability, Spec §US2, research.md]
  > INTENTIONAL GAP — research.md RQ-3 documents the semantic equivalence (single-arg lambda, no body modification); the Assumptions confirm lambda compatibility. Equivalence is further demonstrated by test passage (T008 green). Spec-level formal equivalence proof is not required.
- [x] CHK014 - Is the acceptance scenario for `using Xunit;` retention (US2 scenario 3) testable by a static directive-presence check alone, or does it require build/compilation evidence? [Measurability, Spec §US2]
  > PASS — T007b is a named standalone task: check `using Xunit;` presence by static grep; no compilation evidence needed.

## Scenario Coverage

- [x] CHK015 - Does the spec cover the scenario where `AllSatisfy` is not available at restore time (e.g., NuGet resolution fails to pull 0.3.0 before the conversion is applied)? [Coverage, Edge Case]
  > INTENTIONAL GAP — US1 (T001–T005) is a hard prerequisite: the build gate (SC-002) and test gate (SC-003) must both pass before the conversion phase (T006+) begins; an AllSatisfy-unavailable failure manifests as a T004 build failure and blocks the phase transition automatically.
- [x] CHK016 - Does the spec address whether the routing integration test must pass in isolation or only as part of the full suite run? [Clarity, Coverage, Spec §US2]
  > PASS — SC-003 explicitly requires `dotnet test` green "across Unit, Contract, and Integration projects" — full suite; isolation is not required.
- [x] CHK017 - Does the spec address multi-line `Assert.All` lambda bodies — are single-line-only transformations explicitly assumed, or is multi-line form also in scope? [Coverage, Edge Case, Spec §Edge Cases]
  > INTENTIONAL GAP — Assumptions state "same lambda signature already present inside the existing Assert.All calls"; both known call sites are confirmed single-line; no multi-line form exists in the codebase. The Conversion Catalog explicitly enumerates all two sites.

## Edge Cases

- [x] CHK018 - Is the case where `Assert.All` appears via a fully-qualified name (`Xunit.Assert.All`) or alias covered by FR-007's scan definition? [Ambiguity, Edge Case, Spec §FR-007]
  > PASS — FR-007's scan pattern `Assert\.` matches the `Assert.` substring in any form including `Xunit.Assert.All`; the scan is comprehensive for all known forms.
- [x] CHK019 - Is it specified whether the `using Xunit;` directive could alternatively be replaced by a more targeted directive (e.g., `using static Xunit.Assert;`), or must the exact form be preserved? [Clarity, Edge Case, Spec §FR-006]
  > PASS — FR-006 says the directive "MUST be retained" — the exact `using Xunit;` form must be preserved; replacement with an alias or alternative directive is not permitted.

## Scope Boundary & Constraints

- [x] CHK020 - Is the out-of-scope constraint in FR-008 (xUnit package references unchanged) precise enough to distinguish between a version bump (prohibited) and a reference removal (also prohibited)? [Clarity, Spec §FR-008, Out of Scope]
  > PASS — FR-008 prohibits changes to package references (version bump); Out of Scope explicitly states "Removing xUnit dependencies... are still required" (removal prohibited); both forms are covered.
- [x] CHK021 - Is FR-009 ("no production source files modified") explicit about which directories constitute "production" — does it cover `.props`, `.targets`, and build infrastructure files, or only `.cs` source files under `src/`? [Clarity, Spec §FR-009]
  > PASS — FR-009 now explicitly defines production as "all files under `src/` in the repository root, including `.cs`, `.csproj`, `.props`, and `.targets` files" (added during checklist completion).

## Notes

- Items marked `[Gap]` indicate requirements not explicitly present in the current spec — evaluate whether they are intentionally omitted or need to be added before implementation.
- Check items off as completed: `[x]`
- This checklist covers **User Story 2 only** (Assert.All conversion). See `upgrade.md` for the version-bump gate.
- SC-005 (NC-0001/NC-0002 artifact closure) is explicitly **out of scope** for this checklist per Q3 decision.
