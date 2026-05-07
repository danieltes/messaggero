# Feature Specification: Test Assertion Modernization

**Feature Branch**: `00014-xunit-assertivo-migration`  
**Created**: 2026-05-01  
**Status**: Draft  
**Input**: User description: "Modernize the test suite by replacing supported xUnit assertions with equivalent Assertivo assertions while preserving the original test behavior and assertion semantics. The scope is limited to test projects and test source files where xUnit assertions are used, including updating imports when needed and leaving unsupported or ambiguous assertions unchanged. The refactor does not include changing test names, test structure, test data, the xUnit test framework itself, production code, or test behavior; it also must not remove xUnit dependencies unless they are no longer required after the refactor."

## Clarifications

### Session 2026-05-01

- Q: Which source should be authoritative for deciding supported xUnit assertions and their Assertivo equivalents? -> A: Use a hybrid authority model by starting from Assertivo official mappings and documenting repository-specific exceptions explicitly before conversion.
- Q: Under what condition should xUnit dependencies be removed from test projects? -> A: Keep dependencies by default and remove per project only after confirming no xUnit namespace usage remains and compile/test validation succeeds.
- Q: What conversion policy should apply to non-exact assertion mapping candidates? -> A: Use a two-pass policy that auto-converts only exact 1:1 mappings and emits an explicit review list for non-exact candidates without auto-conversion.
- Q: What scope defines baseline compile and test validation during migration? -> A: Use staged baseline validation where each change set compiles and tests modified projects, then run full repository compile and full test suite before declaring migration complete.
- Q: What output format should the non-exact mapping review list use? -> A: Produce both a machine-readable review list and a human-readable summary.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Migrate Supported Assertions Safely (Priority: P1)

As a maintainer, I want supported legacy test assertions replaced with their approved modern equivalents so the test suite is modernized without changing test outcomes.

**Why this priority**: This delivers the core value of the feature and represents the primary migration objective.

**Independent Test**: Apply the refactor to a representative test project containing supported assertion patterns, then verify that the test project still builds and existing tests keep the same pass/fail outcomes.

**Acceptance Scenarios**:

1. **Given** a test source file with supported assertion patterns, **When** the refactor is applied, **Then** each supported assertion is replaced with the approved equivalent while keeping assertion intent and expected outcome.
2. **Given** a modified test source file that needs updated imports, **When** the refactor is applied, **Then** required imports for the replacement assertions are present and no required imports are removed.

---

### User Story 2 - Preserve Unsupported and Ambiguous Assertions (Priority: P2)

As a maintainer, I want unsupported or ambiguous assertion patterns left untouched so the migration does not introduce semantic risk.

**Why this priority**: Safety is more important than conversion coverage for uncertain cases.

**Independent Test**: Apply the refactor to a file containing both supported and unsupported assertions, then verify only supported assertions are changed.

**Acceptance Scenarios**:

1. **Given** a test source file with unsupported or ambiguous assertions, **When** the refactor is applied, **Then** those assertions remain unchanged.
2. **Given** a file with mixed assertion types, **When** migration completes, **Then** supported assertions are converted and unsupported or ambiguous assertions remain exactly as before.

---

### User Story 3 - Enforce Refactor Scope Boundaries (Priority: P3)

As a maintainer, I want the migration constrained to test projects and test source files so production code and non-test assets are unaffected.

**Why this priority**: Clear boundaries prevent accidental changes outside the intended modernization effort.

**Independent Test**: Run the refactor at repository scope and verify no production code files or non-test projects are changed.

**Acceptance Scenarios**:

1. **Given** repository files that are not test source files, **When** the refactor runs, **Then** those files are unchanged.
2. **Given** test projects that still require xUnit for non-assertion usage, **When** the migration completes, **Then** those dependencies remain present.

### Edge Cases

- A test file uses fully qualified assertion calls, alias imports, or static imports instead of a single common import style.
- A supported assertion includes custom failure messages, tolerance parameters, or collection order constraints that must retain original semantics.
- A single file contains both convertible and non-convertible assertion patterns.
- A test project uses xUnit for attributes, fixtures, or theory data even after assertion conversion.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The refactor MUST target only test projects and test source files that currently contain xUnit assertion calls.
- **FR-002**: For each supported exact 1:1 xUnit assertion pattern in the equivalence catalog, the refactor MUST replace it with the approved Assertivo equivalent.
- **FR-003**: Each converted assertion MUST preserve the original assertion semantics, including compared values, logical expectation, and pass/fail behavior.
- **FR-004**: Unsupported or ambiguous assertion patterns MUST remain unchanged.
- **FR-005**: When a file is modified, required imports for converted assertions MUST be added or updated.
- **FR-006**: Existing imports MUST only be removed if they are no longer required after conversion in that file.
- **FR-007**: The refactor MUST NOT change test names, test structure, test data, or execution flow.
- **FR-008**: The refactor MUST NOT modify production code or non-test projects.
- **FR-009**: xUnit dependencies MUST be kept by default and MAY be removed per test project only after verifying no xUnit namespace usage remains in that project.
- **FR-010**: In files containing both supported and unsupported assertions, the refactor MUST perform partial conversion by changing only supported assertions.
- **FR-011**: The migration MUST treat Assertivo official documentation and examples as the baseline source of assertion equivalence.
- **FR-012**: Repository-specific assertion equivalence exceptions MAY be applied only when explicitly documented with rationale before conversion.
- **FR-013**: For any project where xUnit dependencies are removed, the project MUST compile and its project-level baseline tests MUST pass after removal.
- **FR-014**: The migration MUST use a two-pass conversion approach where only exact 1:1 mappings are auto-converted.
- **FR-015**: Assertion occurrences that are non-exact mapping candidates MUST remain unchanged and be captured for manual follow-up.
- **FR-016**: Validation MUST follow a staged baseline: each migration change set validates compile and test results for modified projects, and migration completion requires full repository compile and full test suite validation.
- **FR-017**: Non-exact mapping candidates MUST be exported in both a machine-readable artifact and a human-readable summary artifact.

### Key Entities *(include if feature involves data)*

- **Assertion Occurrence**: A single assertion usage in test code, categorized as supported, unsupported, or ambiguous.
- **Assertion Equivalence Rule**: A defined mapping that links one supported source assertion pattern to one approved target assertion pattern with equivalent semantics.
- **Migration Scope Unit**: A test source file and its containing test project, used to enforce in-scope and out-of-scope boundaries.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-scope supported assertion occurrences are converted according to approved equivalence rules.
- **SC-002**: 100% of unsupported or ambiguous assertion occurrences remain unchanged after migration.
- **SC-003**: 0 files outside in-scope test projects and test source files are modified by the refactor.
- **SC-004**: For each migration change set, modified projects maintain pre-refactor compile success and test pass rate for those projects, and before completion the full repository compile succeeds and full test suite pass rate matches the pre-refactor baseline.
- **SC-005**: In a reviewer validation sample of at least 30 converted assertions, at least 95% are confirmed to preserve original assertion intent with no behavioral differences.
- **SC-006**: 100% of converted assertion types are traceable to either an Assertivo official mapping or a documented repository-specific exception.
- **SC-007**: 100% of projects with removed xUnit dependencies show zero remaining xUnit namespace usage and successful compile/test validation.
- **SC-008**: 100% of non-exact mapping candidates are listed for manual review, and 0 are auto-converted.
- **SC-009**: For every migration run containing non-exact mapping candidates, both machine-readable and human-readable review artifacts are generated.

## Assumptions

- A maintained assertion equivalence catalog is available before migration execution, seeded from Assertivo official mappings and extended only with documented repository-specific exceptions.
- Test projects and test source files can be identified reliably by existing repository structure and conventions.
- The baseline test run used for comparison is stable before applying the refactor.
- Unsupported or ambiguous assertions are intentionally deferred for separate manual review rather than forced conversion.
