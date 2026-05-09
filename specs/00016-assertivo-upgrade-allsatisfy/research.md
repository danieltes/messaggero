# Research: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Feature**: `00016-assertivo-upgrade-allsatisfy`  
**Phase**: 0 — Research  
**Date**: 2026-05-09

## Research Questions

| # | Question | Source |
|---|----------|--------|
| RQ-1 | Is `Assertivo 0.3.0` published on NuGet? | NuGet flat-container index |
| RQ-2 | What changed between 0.2.0 and 0.3.0? Are there breaking changes? | GitHub Releases |
| RQ-3 | Is the `AllSatisfy` lambda signature compatible with the existing `Assert.All` lambda bodies? | NuGet package docs |

---

## Findings

### RQ-1 — Version Availability

**Decision**: Proceed with the upgrade.  
**Rationale**: `Assertivo 0.3.0` is confirmed available on NuGet. The full published version list is:
`0.1.1`, `0.1.2`, `0.2.0`, `0.3.0`.  
**Alternatives considered**: Waiting for a later release — not needed; 0.3.0 is the target version stated in the spec.

---

### RQ-2 — Breaking Changes between 0.2.0 and 0.3.0

**Decision**: No breaking changes. Proceed with the upgrade without any assertion-level remediation.  
**Rationale**: The GitHub release for `v0.3.0` lists exactly one functional change over `v0.2.0`:
- `Add AllSatisfy collection assertion API` ([#24](https://github.com/danieltes/assertivo/pull/24))

All other PRs in the cumulative changelog are CI/security/infra-only changes:
- Add CodeQL static analysis workflow (#9)
- Add SECURITY.md (#11)
- Commit version bump to Directory.Build.props before tagging (#12)
- Restrict workflow permissions to least privilege (#14)
- Patch csproj version properties on tag creation (#15)
- Update supported version in security policy (#16, #17)
- Remove quickstart link from readme (#19)

No public API removals, signature changes, or behavioral modifications to existing assertion methods.  
**Alternatives considered**: Pinning to 0.2.0 — not needed; 0.3.0 is purely additive.

---

### RQ-3 — AllSatisfy API Signature Compatibility

**Decision**: Lambda bodies require zero modification. Only the call-site wrapper changes.  
**Rationale**: The `AllSatisfy` API confirmed on the NuGet package page:

```csharp
// Single-argument overload (element only)
scores.Should().AllSatisfy(score => score.Should().BeGreaterThanOrEqualTo(10));

// Two-argument overload (element + index)
scores.Should().AllSatisfy((score, index) => score.Should().Be((index + 1) * 10));
```

The two existing `Assert.All` call sites use the single-argument form:

```csharp
// Before (line 80)
Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"));

// Before (line 81)
Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"));
```

The lambda body `m => m.Type.Should().Be(...)` is a single-argument lambda — identical to the
`AllSatisfy(element => ...)` overload. The body does not reference an index, so no modification is needed.

**Alternatives considered**: None — the signatures are a direct mechanical match.

---

## Resolution Summary

| ID | Question | Status | Decision |
|----|----------|--------|----------|
| RQ-1 | Assertivo 0.3.0 available on NuGet? | ✅ Resolved | Yes — published; proceed |
| RQ-2 | Breaking changes 0.2.0 → 0.3.0? | ✅ Resolved | None — additive only |
| RQ-3 | AllSatisfy signature compatible? | ✅ Resolved | Yes — single-arg lambda; body unchanged |

**All NEEDS CLARIFICATION items resolved. Phase 1 may proceed.**
