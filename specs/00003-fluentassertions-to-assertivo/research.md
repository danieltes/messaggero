# Research: Replace FluentAssertions with Assertivo

**Feature**: 00003-fluentassertions-to-assertivo
**Date**: 2026-04-18

## 1. Assertivo API Compatibility

### Decision
Assertivo v0.1.2 provides a fully compatible `.Should()` API surface for every assertion pattern used in the Messaggero test suite.

### Rationale
Source-level verification of Assertivo's GitHub repository confirms all required assertion methods exist with matching signatures. The library uses the same `.Should()` entry-point pattern, `AndConstraint<T>` / `AndWhichConstraint<T, TSubject>` return types for chaining, and `.Which` accessor for drill-down assertions.

### Compatibility Matrix

| Pattern | FluentAssertions Usage | Assertivo Support | Source |
|---------|----------------------|-------------------|--------|
| `.Should().Be(value)` | ObjectAssertions | `ObjectAssertions<T>.Be()` | ObjectAssertions.cs |
| `.Should().BeSameAs(ref)` | ObjectAssertions | `ObjectAssertions<T>.BeSameAs()` | ObjectAssertions.cs |
| `.Should().BeOfType<T>()` | ObjectAssertions | `ObjectAssertions<T>.BeOfType<TTarget>()` | ObjectAssertions.cs |
| `.Should().BeNull()` | ObjectAssertions | `ObjectAssertions<T>.BeNull()` | ObjectAssertions.cs |
| `.Should().NotBeNull()` | ObjectAssertions | `ObjectAssertions<T>.NotBeNull()` | ObjectAssertions.cs |
| `.Should().BeTrue()` | BooleanAssertions | `BooleanAssertions.BeTrue()` | BooleanAssertions.cs |
| `.Should().BeFalse()` | BooleanAssertions | `BooleanAssertions.BeFalse()` | BooleanAssertions.cs |
| `.Should().Contain(str)` | StringAssertions | `StringAssertions.Contain()` | StringAssertions.cs |
| `.Should().NotContain(str)` | StringAssertions | `StringAssertions.NotContain()` | StringAssertions.cs |
| `.Should().NotBeNullOrEmpty()` | StringAssertions | `StringAssertions.NotBeNullOrEmpty()` | StringAssertions.cs |
| `.Should().BeEmpty()` (string) | StringAssertions | `StringAssertions.BeEmpty()` | StringAssertions.cs |
| `.Should().BeGreaterThanOrEqualTo(n)` | NumericAssertions | `NumericAssertions<T>.BeGreaterThanOrEqualTo()` | Numeric/ |
| `.Should().BeLessThan(n)` | NumericAssertions | `NumericAssertions<T>.BeLessThan()` | Numeric/ |
| `.Should().HaveCount(n)` | CollectionAssertions | `GenericCollectionAssertions<T>.HaveCount()` | Collections/ |
| `.Should().ContainSingle()` | CollectionAssertions | `GenericCollectionAssertions<T>.ContainSingle()` | Collections/ |
| `.Should().ContainSingle().Which` | AndWhichConstraint | `AndWhichConstraint<T, TSubject>.Which` | Primitives/ |
| `.Should().Contain(item)` | CollectionAssertions | `GenericCollectionAssertions<T>.Contain()` | Collections/ |
| `.Should().BeEmpty()` (collection) | CollectionAssertions | `GenericCollectionAssertions<T>.BeEmpty()` | Collections/ |
| `.Should().NotBeEmpty()` | CollectionAssertions | Documented in README | README.md |
| `.Should().BeEquivalentTo(...)` | CollectionAssertions | `GenericCollectionAssertions<T>.BeEquivalentTo()` | Collections/ |
| `.Should().AllSatisfy(pred)` | CollectionAssertions | `GenericCollectionAssertions<T>.AllSatisfy()` | Collections/ |
| `.Should().Throw<T>()` | ActionAssertions | `ActionAssertions.Throw<T>()` | Exceptions/ |
| `.Should().ThrowAsync<T>()` | AsyncFunctionAssertions | `AsyncFunctionAssertions.ThrowAsync<T>()` | Exceptions/ |
| `.Throw<T>().Which` | AndWhichConstraint | `AndWhichConstraint<T, TSubject>.Which` | Primitives/ |

### Alternatives Considered
- **xUnit Assert**: Native but verbose, no fluent chaining, no `.Which` drill-down. Suitable only as fallback for missing patterns.
- **Shouldly**: Fluent assertions but uses `ShouldBe()` pattern — would require significant rewrite of all assertion calls.

## 2. BeEquivalentTo Comparison Semantics

### Decision
Assertivo's `BeEquivalentTo()` is order-independent and frequency-aware, matching FluentAssertions' default behavior for simple collections.

### Rationale
Source code inspection confirms Assertivo uses a frequency-aware matching algorithm (removes matched items from a remaining list). The README explicitly states "order-independent". This matches FluentAssertions' default `BeEquivalentTo()` behavior for flat collections without custom options.

### Alternatives Considered
- N/A — semantics match. No action needed.

## 3. Version Pinning Strategy

### Decision
Pin to exact version `0.1.2` in all `.csproj` files.

### Rationale
Assertivo is pre-1.0 (semver allows breaking changes in minor releases). Exact pinning prevents surprise breakage. Updates will be manual after testing.

### Alternatives Considered
- **Floating range `0.1.*`**: Rejected — pre-1.0 patches could introduce breaking changes.
- **Minor range `[0.1.2, 0.2.0)`**: Rejected — still risky for pre-1.0 library.

## 4. Incompatible Pattern Fallback Strategy

### Decision
Rewrite using native xUnit asserts immediately. Do not block on upstream.

### Rationale
Per spec clarification. The migration must be self-contained. Based on the compatibility matrix above, no fallback is expected to be needed — all patterns are covered.

### Alternatives Considered
- **Block on upstream issue**: Rejected — would delay migration indefinitely for a v0.1.x library.
- **Rewrite + upstream issue**: Over-engineering — only open issues if actual gaps are discovered.

## 5. Target Framework Compatibility

### Decision
Assertivo targets .NET 10.0, which is compatible with this project's `net10.0` target framework.

### Rationale
Both `Directory.Build.props` (Messaggero) and Assertivo's build prerequisites specify .NET 10 SDK. No TFM conflicts.

### Alternatives Considered
- N/A — direct match. No action needed.
