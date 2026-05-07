# PR Review Checklist: Test Assertion Modernization

**Purpose**: Fast reviewer gate to assess requirement quality and cross-artifact consistency before implementation or merge
**Created**: 2026-05-01
**Feature**: [spec.md](../spec.md)
**Depth**: Standard PR reviewer gate
**Focus**: Semantic equivalence, scope boundaries, non-exact artifacts, xUnit dependency retention/removal

## Requirement Completeness

- [x] CHK001 Are in-scope boundaries explicitly limited to test projects and test source files, with production code excluded? [Completeness, Spec Â§FR-001, Spec Â§FR-008]
- [x] CHK002 Are approved exact mapping rules documented and linked to a defined authority source? [Completeness, Spec Â§FR-002, Spec Â§FR-011]
- [x] CHK003 Are unsupported, ambiguous, and non-exact candidates explicitly required to remain unchanged? [Completeness, Spec Â§FR-004, Spec Â§FR-015]
- [x] CHK004 Are dual non-exact review artifacts required (machine-readable and human-readable) with expected content defined? [Completeness, Spec Â§FR-017, Spec Â§SC-009, Contract Non-Exact Â§1-Â§3]
- [x] CHK005 Are xUnit dependency removal preconditions and required validation outcomes defined per project? [Completeness, Spec Â§FR-009, Spec Â§FR-013]

## Requirement Clarity and Measurability

- [x] CHK006 Is the term exact 1:1 mapping defined with objective criteria that different reviewers can apply consistently? [Clarity, Spec Â§FR-002, Contract Assertion Mapping Â§2]
- [x] CHK007 Is no behavior change expressed with measurable acceptance signals rather than qualitative wording only? [Measurability, Spec Â§FR-003, Spec Â§SC-004, Spec Â§SC-005]
- [x] CHK008 Is manual follow-up for non-exact candidates defined with required status fields and closure expectations? [Clarity, Spec Â§FR-015, Data Model Â§NonExactCandidateRecord]
- [x] CHK009 Is staged baseline validation defined with clear per-change-set and pre-completion gates? [Clarity, Spec Â§FR-016, Spec Â§SC-004, Quickstart Â§Step 6-Â§Step 7]

## Requirement Consistency and Traceability

- [x] CHK010 Do two-pass conversion requirements align with success criteria that forbid auto-converting non-exact candidates? [Consistency, Spec Â§FR-014, Spec Â§FR-015, Spec Â§SC-008]
- [x] CHK011 Do artifact requirements in spec align with schema and consistency rules in contracts? [Consistency, Spec Â§FR-017, Contract Non-Exact Â§2-Â§4]
- [x] CHK012 Do dependency-removal requirements align with the quickstart dependency-removal workflow? [Consistency, Spec Â§FR-009, Spec Â§FR-013, Quickstart Â§Step 8]
- [x] CHK013 Are conversion rules traceable across spec, research, and mapping contract without missing or conflicting entries? [Traceability, Spec Â§FR-002, Research Â§2, Contract Assertion Mapping Â§3]

## Scenario and Edge Coverage

- [x] CHK014 Are requirements complete for mixed files containing both convertible and non-convertible assertion patterns? [Coverage, Spec Â§FR-010]
- [x] CHK015 Are edge-case forms (alias, static-import, fully-qualified assertions) covered by explicit classification requirements? [Coverage, Spec Â§Edge Cases, Data Model Â§AssertionOccurrence]
- [x] CHK016 Are requirements explicit for projects with zero remaining Assert calls but continued xUnit attribute or framework usage? [Coverage, Spec Â§FR-009, Quickstart Â§Step 8]

## Dependencies, Assumptions, and Open Gaps

- [x] CHK017 Are assumptions about maintaining the equivalence catalog paired with fallback requirements if the catalog is incomplete or stale? [Assumption, Spec Â§Assumptions, Gap]
- [x] CHK018 Are external dependency assumptions (Assertivo docs/version behavior) linked to mitigation or revalidation expectations? [Dependency, Assumption, Research Â§1, Gap]
- [x] CHK019 Is project-level baseline tests unambiguous about which test subsets are acceptable for staged validation? [Ambiguity, Spec Â§FR-013, Spec Â§FR-016]
- [x] CHK020 Is selected baseline tests unambiguous regarding selection criteria and reviewer reproducibility? [Ambiguity, Spec Â§FR-013, Gap]

## Notes

- This checklist validates requirement quality, not implementation behavior.
- Use together with [assertions.md](assertions.md) when a full audit-grade pass is required.
