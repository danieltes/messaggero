# Implementation Plan: Replace FluentAssertions with Assertivo

**Branch**: `00003-fluentassertions-to-assertivo` | **Date**: 2026-04-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/00003-fluentassertions-to-assertivo/spec.md`

## Summary

Migrate all test assertion infrastructure from FluentAssertions 8.3.0 (commercially-licensed) to Assertivo 0.1.2 (MIT-licensed). The migration touches 3 test projects and 18 test source files. The approach is: swap package references, replace `using` directives, verify API compatibility across all assertion patterns (boolean, string, numeric, collection, exception, async exception, drill-down), and rewrite any incompatible patterns using native xUnit asserts.

## Technical Context

**Language/Version**: C# / .NET 10.0 (`net10.0`, `LangVersion` latest)
**Primary Dependencies**: xunit 2.9.3, NSubstitute 5.3.0, FluentAssertions 8.3.0 → Assertivo 0.1.2, Microsoft.Extensions.* 10.0.0-preview.3
**Storage**: N/A
**Testing**: xunit 2.9.3 + xunit.runner.visualstudio 3.0.2 + Microsoft.NET.Test.Sdk 17.14.0
**Target Platform**: .NET 10.0 (cross-platform library)
**Project Type**: Library (broker-agnostic messaging)
**Performance Goals**: N/A for this migration (no runtime code changes)
**Constraints**: `TreatWarningsAsErrors` is enabled globally — zero new warnings allowed
**Scale/Scope**: 3 test projects, 18 test files, ~1 assertion library swap

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Gate | Status | Notes |
|---------|------|--------|-------|
| I. Spec-First Development | Specification exists before code | PASS | spec.md written and clarified |
| II. Code Quality Standards | No breaking changes to public API | PASS | No public API changes — test-only migration |
| III. Testing Standards | Tests must pass; no regressions | PASS | FR-003 mandates zero regressions |
| IV. Developer Experience | Documentation updated | PASS | No user-facing doc changes needed — internal test dependency |
| V. Performance | No unmeasured regressions | PASS | No runtime code changes; benchmarks project untouched |
| VI. Compatibility | Dependency change assessed | PASS | Research will verify Assertivo API compatibility |
| VII. Security | Secure defaults maintained | PASS | No security surface changes |

**Gate result: PASS** — no violations. Proceeding to Phase 0.

### Post-Design Re-Check (after Phase 1)

| Article | Gate | Status | Notes |
|---------|------|--------|-------|
| I. Spec-First Development | Specification exists before code | PASS | spec.md completed with clarifications |
| II. Code Quality Standards | No breaking changes to public API | PASS | No public API changes — test-only migration |
| III. Testing Standards | Tests must pass; no regressions | PASS | FR-003; research confirms full API compatibility |
| IV. Developer Experience | Documentation updated | PASS | quickstart.md created for migration steps |
| V. Performance | No unmeasured regressions | PASS | No runtime code changes; benchmarks untouched |
| VI. Compatibility | Dependency change assessed | PASS | research.md documents full compatibility matrix |
| VII. Security | Secure defaults maintained | PASS | MIT license replaces commercial — improvement |

**Post-design gate result: PASS** — no violations. No change from initial check.

## Project Structure

### Documentation (this feature)

```text
specs/00003-fluentassertions-to-assertivo/
├── plan.md              # This file
├── research.md          # Phase 0: Assertivo API compatibility research
├── data-model.md        # Phase 1: Affected files and assertion pattern inventory
├── quickstart.md        # Phase 1: Migration steps
├── contracts/           # Phase 1: N/A — no external interface changes
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
tests/
├── Messaggero.Tests.Unit/           # 7 test files, FluentAssertions → Assertivo
│   ├── Messaggero.Tests.Unit.csproj
│   ├── Configuration/
│   ├── Examples/
│   ├── Hosting/
│   ├── Observability/
│   └── Routing/
├── Messaggero.Tests.Integration/    # 6 test files, FluentAssertions → Assertivo
│   ├── Messaggero.Tests.Integration.csproj
│   └── *.cs
├── Messaggero.Tests.Contract/       # 5 test files, FluentAssertions → Assertivo
│   ├── Messaggero.Tests.Contract.csproj
│   └── *.cs
└── Messaggero.Tests.Benchmarks/     # Out of scope (no FluentAssertions)
```

**Structure Decision**: No structural changes. The migration modifies only `.csproj` package references and `using` directives within the existing test project layout.

## Complexity Tracking

No constitution violations — table not applicable.
