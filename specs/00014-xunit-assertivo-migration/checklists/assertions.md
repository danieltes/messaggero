# Assertions Checklist: Test Assertion Modernization

**Purpose**: Validate requirements quality and cross-artifact consistency for the xUnit-to-Assertivo modernization feature
**Created**: 2026-05-01
**Feature**: [spec.md](../spec.md)
**Depth**: Audit-grade compliance gate
**Audience**: Reviewer and release gate owners

## Requirement Completeness

- [x] CHK001 Are in-scope boundaries explicitly defined for test projects/files and explicitly excluding production and benchmark paths? [Completeness, Spec Â§FR-001, Spec Â§FR-008, Plan Â§Project Structure]
- [x] CHK002 Are all approved exact mapping rules enumerated in one authoritative place with rule IDs? [Completeness, Spec Â§FR-002, Contract Assertion Mapping Â§3]
- [x] CHK003 Are requirements defined for each non-exact class (non-exact, ambiguous, unsupported) and the mandated unchanged behavior? [Completeness, Spec Â§FR-004, Spec Â§FR-015, Data Model Â§AssertionOccurrence]
- [x] CHK004 Are dual-output artifact requirements fully specified including machine-readable and human-readable formats? [Completeness, Spec Â§FR-017, Spec Â§SC-009, Contract Non-Exact Â§1-Â§3]
- [x] CHK005 Are xUnit dependency retention/removal preconditions and postconditions fully documented per project? [Completeness, Spec Â§FR-009, Spec Â§FR-013]

## Requirement Clarity

- [x] CHK006 Is "exact 1:1 mapping" defined with objective decision criteria that different reviewers would apply the same way? [Clarity, Spec Â§FR-002, Contract Assertion Mapping Â§2]
- [x] CHK007 Is "repository-specific exception" bounded by clear approval and recording requirements? [Clarity, Spec Â§FR-012, Research Â§1]
- [x] CHK008 Is "project-level baseline tests" precise about which commands constitute the required baseline? [Ambiguity, Spec Â§FR-013, Quickstart Â§Step 6]
- [x] CHK009 Is "manual follow-up" defined with required owner, status values, and closure criteria? [Ambiguity, Spec Â§FR-015, Data Model Â§NonExactCandidateRecord]

## Requirement Consistency

- [x] CHK010 Do two-pass requirements align with success criteria that forbid auto-conversion of non-exact candidates? [Consistency, Spec Â§FR-014, Spec Â§FR-015, Spec Â§SC-008]
- [x] CHK011 Do artifact generation requirements in spec align with artifact schema and consistency rules in contracts? [Consistency, Spec Â§FR-017, Contract Non-Exact Â§2-Â§4]
- [x] CHK012 Do dependency-removal rules in spec align with the optional removal workflow in quickstart? [Consistency, Spec Â§FR-009, Spec Â§FR-013, Quickstart Â§Step 8]
- [x] CHK013 Are mapping rules consistent across spec, research, and the assertion-mapping contract without missing or conflicting entries? [Consistency, Spec Â§FR-002, Research Â§2, Contract Assertion Mapping Â§3]

## Acceptance Criteria Quality

- [x] CHK014 Can SC-001 be objectively measured with a deterministic count method for "in-scope supported occurrences"? [Measurability, Spec Â§SC-001, Data Model Â§Baseline Inventory]
- [x] CHK015 Can SC-004 be objectively validated for both per-change-set and full-repository stages? [Measurability, Spec Â§SC-004, Spec Â§FR-016, Quickstart Â§Step 6-Â§Step 7]
- [x] CHK016 Is SC-005 sampling defined precisely enough to avoid reviewer-dependent interpretation? [Gap, Spec Â§SC-005]
- [x] CHK017 Is SC-009 measurable with explicit pass/fail checks for artifact existence and cross-file consistency? [Measurability, Spec Â§SC-009, Contract Non-Exact Â§4-Â§5]

## Scenario Coverage

- [x] CHK018 Are primary-flow requirements complete for exact conversion, import updates, and semantics preservation? [Coverage, Primary Flow, Spec Â§FR-002, Spec Â§FR-003, Spec Â§FR-005]
- [x] CHK019 Are alternate-flow requirements complete for mixed files containing convertible and non-convertible assertions? [Coverage, Alternate Flow, Spec Â§FR-010]
- [x] CHK020 Are exception-flow requirements defined for unresolved mapping decisions or conflicting rule interpretations? [Gap, Exception Flow, Spec Â§FR-012]
- [x] CHK021 Are recovery-flow requirements defined for reverting or containing changes after failed staged validation? [Gap, Recovery Flow, Spec Â§FR-016]

## Edge Case Coverage

- [x] CHK022 Are alias, static-import, and fully-qualified assertion forms covered by explicit classification requirements? [Edge Case, Spec Â§Edge Cases, Data Model Â§AssertionOccurrence]
- [x] CHK023 Are custom-message, tolerance, and order-sensitive assertion variants explicitly classified as exact or non-exact? [Edge Case, Spec Â§Edge Cases, Research Â§2]
- [x] CHK024 Are lambda-based assertions (for example `Assert.All`) explicitly treated as non-exact candidates? [Edge Case, Spec Â§FR-015, Research Â§2, Contract Assertion Mapping Â§4]
- [x] CHK025 Are requirements defined for projects with no remaining `Assert.*` calls but continued xUnit attribute usage? [Edge Case, Spec Â§FR-009, Quickstart Â§Step 8]

## Non-Functional Requirements

- [x] CHK026 Are traceability requirements sufficient to map every conversion to an authority source or documented exception? [Non-Functional, Traceability, Spec Â§SC-006]
- [x] CHK027 Are repeatability requirements defined so repeated scans produce stable candidate IDs and counts? [Non-Functional, Contract Non-Exact Â§4, Gap]
- [x] CHK028 Are auditability requirements defined for retaining review decisions on non-exact candidates over time? [Non-Functional, Data Model Â§NonExactCandidateRecord, Gap]
- [x] CHK029 Is the expected quality bar for staged-validation diagnostics (failure reporting granularity) explicitly specified? [Non-Functional, Spec Â§FR-016, Gap]

## Dependencies & Assumptions

- [x] CHK030 Are critical assumptions linked to fallback behavior if the mapping catalog is incomplete or stale? [Assumption, Spec Â§Assumptions, Research Â§1]
- [x] CHK031 Are external dependency assumptions (Assertivo docs and version stability) explicitly documented with mitigation? [Dependency, Assumption, Research Â§1, Gap]
- [x] CHK032 Are environment prerequisites (SDK, restore/build/test readiness) consistently specified across plan and quickstart? [Dependency, Consistency, Plan Â§Technical Context, Quickstart Â§Prerequisites]
- [x] CHK033 Is ownership for maintaining equivalence rules and artifact schema explicitly assigned? [Dependency, Gap, Contract Assertion Mapping Â§1, Contract Non-Exact Â§2]

## Ambiguities & Conflicts

- [x] CHK034 Do any requirements conflict between "no behavior change" and use of repository-specific mapping exceptions? [Conflict, Spec Â§FR-003, Spec Â§FR-012]
- [x] CHK035 Is the term "baseline" used consistently across spec, plan, and quickstart stages? [Ambiguity, Spec Â§SC-004, Plan Â§Summary, Quickstart Â§Step 1, Quickstart Â§Step 6, Quickstart Â§Step 7]
- [x] CHK036 Is out-of-scope language consistent for production code changes, framework migration, and benchmark project impact? [Consistency, Spec Â§FR-008, Plan Â§Project Structure, Quickstart Â§Goal]

## Notes

- Checklist emphasis selected: assertion semantic equivalence, scope boundary clarity, non-exact artifact requirements, and xUnit dependency retention/removal requirements.
- Items are requirement-quality checks, not implementation test cases.
