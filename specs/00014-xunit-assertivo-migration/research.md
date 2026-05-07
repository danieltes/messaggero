# Research: Modernize xUnit Assertions with Assertivo

**Feature**: 00014-xunit-assertivo-migration
**Date**: 2026-05-01

## 1. Assertion Mapping Authority

### Decision
Use a hybrid authority model: Assertivo official documentation/examples are the baseline mapping source, and repository-specific exceptions are allowed only when documented with rationale before conversion.

### Rationale
- Aligns with clarified requirement from spec session.
- Keeps conversion semantics grounded in upstream intent while allowing local edge-case handling.
- Enables traceability for SC-006 and predictable code review.

### Alternatives considered
- Assertivo-only without local exceptions: rejected because repository context may include unavoidable local patterns.
- Case-by-case ad hoc mapping: rejected because it weakens consistency and auditability.

## 2. Exact 1:1 Mapping Catalog for Current xUnit Usage

### Decision
Auto-convert only the exact mappings below; all other patterns remain unchanged and are emitted as non-exact candidates.

| xUnit assertion pattern | Assertivo target pattern | Auto-convert |
|---|---|---|
| `Assert.Equal(expected, actual)` | `actual.Should().Be(expected)` | Yes |
| `Assert.Single(collection)` | `collection.Should().ContainSingle()` | Yes |
| `Assert.Empty(collection)` | `collection.Should().BeEmpty()` | Yes |
| `Assert.NotNull(value)` | `value.Should().NotBeNull()` | Yes |
| `Assert.Contains(item, collection)` | `collection.Should().Contain(item)` | Yes |
| `Assert.Throws<T>(action)` | `action.Should().Throw<T>()` | Yes |
| `Assert.ThrowsAsync<T>(asyncAction)` | `asyncAction.Should().ThrowAsync<T>()` | Yes |
| `Assert.Equivalent(expected, actual)` | `actual.Should().BeEquivalentTo(expected)` | Yes (default semantics only) |
| `Assert.All(collection, assertion)` | `collection.Should().AllSatisfy(...)` | No (non-exact candidate) |

### Rationale
- Matches the clarified two-pass policy: exact mappings only in automation/refactor pass.
- Reduces semantic risk for lambda-based and context-sensitive assertions.
- Fits current repository scan: 37 xUnit assertions across 12 files, with `All` only in scenarios where lambda semantics can vary.

### Alternatives considered
- Convert all xUnit assertions including non-exact patterns in one pass: rejected due higher regression risk.
- Keep all xUnit asserts unchanged: rejected because it does not satisfy modernization goals.

## 3. Non-Exact Candidate Handling

### Decision
Treat non-exact patterns as review candidates. Do not auto-convert them. Emit both machine-readable and human-readable artifacts.

### Rationale
- Directly satisfies FR-015, FR-017, and SC-008/SC-009.
- Keeps conversion deterministic and review-friendly.
- Supports future incremental migration without blocking current safe refactor.

### Alternatives considered
- Human-readable report only: rejected, not sufficient for automation and drift checks.
- Machine-readable report only: rejected, lowers reviewer ergonomics.

## 4. xUnit Dependency Retention Strategy

### Decision
Keep xUnit dependencies by default. Remove per project only if no xUnit namespace usage remains and compile/test validation passes for that project.

### Rationale
- xUnit is still needed for `[Fact]`, `[Theory]`, and framework runtime behavior unless proven otherwise.
- Avoids accidental breakage from overly aggressive package cleanup.
- Satisfies clarified dependency-removal policy and FR-009/FR-013.

### Alternatives considered
- Never remove xUnit dependencies in this feature: rejected because the spec allows removal when no longer needed.
- Remove once xUnit assertion calls are gone: rejected because non-assertion xUnit usage would still remain.

## 5. Validation Strategy (Staged Baseline)

### Decision
Use staged validation: for each change set validate modified projects (build + tests), and before completion run full repository build and full test suite.

### Rationale
- Balances fast feedback with end-to-end confidence.
- Matches clarified baseline policy in spec and FR-016.
- Creates clear release gate behavior for this migration.

### Alternatives considered
- Full suite on every change set: rejected as slower feedback loop for iterative refactor.
- Modified-project validation only: rejected because it can miss cross-project regressions.

## 6. Scope and File Inventory Baseline

### Decision
Treat the following as in-scope baseline inventory for migration planning:
- 43 test `.cs` files scanned.
- 12 files contain xUnit `Assert.*` usage.
- 37 xUnit assertion calls total by project: Contract 15, Integration 11, Unit 11.

### Rationale
- Derived from repository scan and used as measurable planning baseline.
- Supports scale/scope estimates and post-implementation verification.

### Alternatives considered
- Estimate scope from manual spot checks: rejected as error-prone and non-repeatable.
