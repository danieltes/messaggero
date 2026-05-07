# Implementation Plan: Modernize xUnit Assertions with Assertivo

**Branch**: `00014-xunit-assertivo-migration` | **Date**: 2026-05-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/00014-xunit-assertivo-migration/spec.md`

## Summary

Modernize the current mixed assertion style in test code by replacing supported exact 1:1 xUnit `Assert.*` calls with Assertivo fluent assertions while preserving behavior and test outcomes. The implementation uses a two-pass strategy: auto-convert only exact mappings, and generate review artifacts for non-exact candidates (machine-readable plus human-readable) without auto-conversion. Scope is limited to test projects and test source files.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`)  
**Primary Dependencies**: xunit.v3 3.2.2, Assertivo 0.2.0, Microsoft.NET.Test.Sdk 18.5.0, xunit.runner.visualstudio 3.1.5, NSubstitute 5.3.0, Testcontainers.* 4.11.0  
**Storage**: N/A  
**Testing**: xUnit v3 test projects (`Unit`, `Integration`, `Contract`) with staged validation (`dotnet build` + targeted `dotnet test`, then full suite)  
**Target Platform**: .NET 10, cross-platform library and tests  
**Project Type**: Library with multiple test projects  
**Performance Goals**: No production runtime impact; maintain baseline compile/test outcomes throughout migration  
**Constraints**: Test-only scope; no production code edits; preserve test names/structure/data/behavior; auto-convert exact 1:1 mappings only; leave unsupported/ambiguous unchanged; keep xUnit dependencies by default  
**Scale/Scope**: 43 test `.cs` files scanned; 12 files contain xUnit `Assert.*` usages; 37 xUnit assert calls total across Contract (15), Integration (11), and Unit (11)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Requirement | Status | Notes |
|---|---|---|---|
| I. Spec-First Development | Specification exists and is complete before implementation | PASS | `spec.md` exists with 5 clarifications and measurable criteria |
| II. Code Quality Standards | Preserve API stability and maintainability | PASS | Test-only refactor; no public API or production behavior changes |
| III. Testing Standards | Testing is a release gate; no regressions | PASS | Plan enforces staged validation plus full compile/full suite gate before completion |
| IV. Developer Experience Consistency | Tooling/docs consistency and clear diagnostics | PASS | quickstart and contracts define repeatable migration + review artifact format |
| V. Performance and Throughput | No unmeasured regressions for affected scope | PASS | No runtime path changes; validation focuses on compile/test parity |
| VI. Compatibility and Reliability | Dependency and semantic compatibility assessed | PASS | Research defines authoritative mapping, exact-vs-non-exact policy, and xUnit dependency retention checks |
| VII. Security and Operational Safety | Safe defaults and no sensitive data leakage | PASS | No security surface expansion; log-scrub tests remain in scope and unchanged in behavior |

**Gate result: PASS** — no violations before Phase 0.

### Post-Design Re-Check (after Phase 1)

| Article | Requirement | Status | Notes |
|---|---|---|---|
| I. Spec-First Development | Specification exists and is complete before implementation | PASS | plan/research/data-model/contracts/quickstart align to clarified spec |
| II. Code Quality Standards | Preserve API stability and maintainability | PASS | Data model and contracts constrain changes to assertions/imports only |
| III. Testing Standards | Testing is a release gate; no regressions | PASS | quickstart codifies per-change-set and final full-suite validation |
| IV. Developer Experience Consistency | Tooling/docs consistency and clear diagnostics | PASS | Dual-format non-exact review artifacts defined for deterministic review flow |
| V. Performance and Throughput | No unmeasured regressions for affected scope | PASS | No runtime code paths changed; benchmark project remains out of scope |
| VI. Compatibility and Reliability | Dependency and semantic compatibility assessed | PASS | Explicit policy for keeping/removing xUnit dependencies per project validation |
| VII. Security and Operational Safety | Safe defaults and no sensitive data leakage | PASS | No new operational risk introduced by design |

**Post-design gate result: PASS** — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/00014-xunit-assertivo-migration/
├── plan.md                                 # This file
├── research.md                             # Phase 0 output
├── data-model.md                           # Phase 1 output
├── quickstart.md                           # Phase 1 output
├── contracts/
│   ├── assertion-mapping-contract.md       # Exact mapping contract
│   └── non-exact-review-artifacts.md       # Machine + human artifact contract
├── checklists/
│   └── requirements.md
└── tasks.md                                # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
tests/
├── Messaggero.Tests.Unit/
│   ├── Configuration/
│   ├── Hosting/
│   └── Routing/
├── Messaggero.Tests.Integration/
│   ├── AdapterIsolationTests.cs
│   ├── FanInIntegrationTests.cs
│   ├── FanOutFailureTests.cs
│   └── RoutingIntegrationTests.cs
├── Messaggero.Tests.Contract/
│   ├── AckNackContractTests.cs
│   ├── BackpressureContractTests.cs
│   ├── LifecycleContractTests.cs
│   └── PublishContractTests.cs
└── Messaggero.Tests.Benchmarks/            # Out of scope

src/                                        # Out of scope for this feature
```

**Structure Decision**: Keep existing repository structure unchanged. Modify only in-scope test source files and imports under `tests/Messaggero.Tests.Unit`, `tests/Messaggero.Tests.Integration`, and `tests/Messaggero.Tests.Contract`. Production source under `src/` and benchmark tests remain unchanged.

## Complexity Tracking

No constitution violations identified; no complexity exemptions required.
