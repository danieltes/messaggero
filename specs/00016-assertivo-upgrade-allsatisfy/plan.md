# Implementation Plan: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Branch**: `00016-assertivo-upgrade-allsatisfy` | **Date**: 2026-05-09 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `/specs/00016-assertivo-upgrade-allsatisfy/spec.md`

## Summary

Upgrade `Assertivo` from `0.2.0` to `0.3.0` in all three test projects
(`Unit`, `Contract`, `Integration`), then replace the two remaining `Assert.All` call
sites in `RoutingIntegrationTests.cs` with the new `AllSatisfy` collection assertion
introduced in 0.3.0. This closes the `Assert.All` non-exact candidates
(`NC-0001`, `NC-0002`) left pending by the 00014 migration.

Research confirmed: 0.3.0 is purely additive (one new API: `AllSatisfy`), no breaking
changes to existing assertion chains, and the single-argument lambda bodies already
inside `Assert.All` map directly to `AllSatisfy` without modification.

## Technical Context

**Language/Version**: C# / .NET 10.0  
**Primary Dependencies**: Assertivo 0.3.0 (was 0.2.0), xUnit v3 3.2.2 (unchanged)  
**Storage**: N/A  
**Testing**: xUnit v3 / Assertivo  
**Target Platform**: .NET 10.0 (`net10.0`)  
**Project Type**: library test suite (no production code changes)  
**Performance Goals**: N/A  
**Constraints**: Zero build warnings; all tests green after change  
**Scale/Scope**: 3 `.csproj` files (1 line each) + 2 call sites in 1 `.cs` file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Gate | Status | Notes |
|---------|------|--------|-------|
| I — Spec-First | Specification exists with acceptance criteria | ✅ PASS | spec.md written and reviewed |
| II — Code Quality | Changes are minimal, targeted, and backward-compatible | ✅ PASS | 5 mechanical edits; no new abstractions |
| III — Testing | Existing test suite validates the change; no test patterns altered | ✅ PASS | All three projects run; routing test semantics unchanged |
| IV — DX Consistency | `Should()` chain style maintained throughout | ✅ PASS | `AllSatisfy` follows identical `.Should()` entry-point pattern |
| V — Performance | N/A — no performance-sensitive paths involved | ✅ PASS (N/A) | Test-only change |
| VI — Compatibility | Upgrade is within minor version; no breaking changes confirmed by research | ✅ PASS | RQ-2 resolved: 0.3.0 is additive-only |
| VII — Security | N/A — no transport, credential, or data-handling code changed | ✅ PASS (N/A) | Test tooling upgrade only |

**Post-Design Re-check**: All gates pass. No violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/00016-assertivo-upgrade-allsatisfy/
├── plan.md              # This file
├── research.md          # Phase 0 output — NuGet/GitHub version research
├── data-model.md        # Phase 1 output — N/A (no domain entities)
├── quickstart.md        # Phase 1 output — implementation steps
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
tests/
├── Messaggero.Tests.Unit/
│   └── Messaggero.Tests.Unit.csproj          ← bump Assertivo 0.2.0 → 0.3.0
├── Messaggero.Tests.Contract/
│   └── Messaggero.Tests.Contract.csproj      ← bump Assertivo 0.2.0 → 0.3.0
└── Messaggero.Tests.Integration/
    ├── Messaggero.Tests.Integration.csproj   ← bump Assertivo 0.2.0 → 0.3.0
    └── RoutingIntegrationTests.cs            ← replace 2 Assert.All call sites

specs/00014-xunit-assertivo-migration/artifacts/
└── non-exact-candidates.md                   ← update NC-0001, NC-0002 → Resolved
```

**Structure Decision**: No new files in `src/`. All changes are confined to `tests/`
project files and one test class. The `specs/00014` artifact update is a closure
record — it is not a source code file and requires no build step.

## Complexity Tracking

*No constitution violations. This section is intentionally blank.*
