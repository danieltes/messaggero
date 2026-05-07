# Contract: Assertion Mapping Rules

**Feature**: 00014-xunit-assertivo-migration
**Date**: 2026-05-01

This contract defines which xUnit assertion patterns can be auto-converted and the constraints that must hold for a valid conversion.

## 1. Source of Truth

1. Assertivo official documentation/examples are the baseline authority.
2. Repository-specific exceptions are allowed only when documented with rationale.
3. Every converted assertion must be traceable to either baseline authority or a documented exception.

## 2. Auto-Conversion Eligibility

An assertion occurrence is auto-convertible only when all conditions hold:

1. The source pattern matches an approved exact 1:1 rule.
2. Argument semantics map directly without behavior reinterpretation.
3. Conversion does not require structural test rewrites.
4. Conversion does not change test intent or expected outcome.

If any condition fails, classify as non-exact and do not auto-convert.

## 3. Exact Mapping Rules (Approved)

| Rule ID | xUnit source pattern | Assertivo target pattern |
|---|---|---|
| `XR-EQUAL-001` | `Assert.Equal(expected, actual)` | `actual.Should().Be(expected)` |
| `XR-SINGLE-001` | `Assert.Single(collection)` | `collection.Should().ContainSingle()` |
| `XR-EMPTY-001` | `Assert.Empty(collection)` | `collection.Should().BeEmpty()` |
| `XR-NOTNULL-001` | `Assert.NotNull(value)` | `value.Should().NotBeNull()` |
| `XR-CONTAINS-001` | `Assert.Contains(item, collection)` | `collection.Should().Contain(item)` |
| `XR-THROWS-001` | `Assert.Throws<T>(action)` | `action.Should().Throw<T>()` |
| `XR-THROWSASYNC-001` | `Assert.ThrowsAsync<T>(asyncAction)` | `asyncAction.Should().ThrowAsync<T>()` |
| `XR-EQUIVALENT-001` | `Assert.Equivalent(expected, actual)` | `actual.Should().BeEquivalentTo(expected)` |

## 4. Explicit Non-Exact Rules

| xUnit source pattern | Reason |
|---|---|
| `Assert.All(collection, assertion)` | Lambda-body semantics can vary; requires manual review to avoid behavioral drift |

## 5. Import and Framework Constraints

1. `using Assertivo;` must exist where converted fluent assertions are used.
2. `using Xunit;` remains required as long as any xUnit usage remains in the file or project.
3. xUnit package dependencies are kept by default; removal is allowed only after zero namespace usage plus compile/test validation.

## 6. Validation Requirements

1. Per change set: build + tests for modified projects must pass.
2. Completion gate: full repository build + full test suite must pass.
3. Unsupported, ambiguous, and non-exact candidates remain unchanged and must be exported to review artifacts.
