# Data Model: Modernize xUnit Assertions with Assertivo

**Feature**: 00014-xunit-assertivo-migration
**Date**: 2026-05-01

## Overview

This feature does not introduce runtime domain entities. The relevant data model is the migration model used to classify assertion occurrences, apply safe conversions, track dependency decisions, and emit review artifacts for non-exact candidates.

## Entities

### 1. AssertionOccurrence

Represents one in-scope `Assert.*` call discovered in test source.

| Field | Type | Description | Validation |
|---|---|---|---|
| `Id` | string | Stable identifier (for example file+line hash) | Required, unique per run |
| `Project` | enum | `Unit`, `Integration`, or `Contract` | Required |
| `FilePath` | string | Repository-relative test file path | Must be under `tests/` |
| `LineNumber` | integer | Source line of assertion | `> 0` |
| `Method` | enum | xUnit method name (`Equal`, `Single`, etc.) | Required |
| `SourceSnippet` | string | Original assertion expression | Required |
| `Classification` | enum | `Exact`, `NonExact`, `Unsupported`, `Ambiguous` | Required |
| `RuleId` | string? | Linked equivalence rule ID for exact matches | Required when `Classification=Exact` |
| `Status` | enum | `Discovered`, `Converted`, `Unchanged`, `Validated` | Required |

### 2. AssertionEquivalenceRule

Defines one approved source-to-target mapping.

| Field | Type | Description | Validation |
|---|---|---|---|
| `RuleId` | string | Rule identifier (for example `XR-EQUAL-001`) | Required, unique |
| `SourcePattern` | string | xUnit assertion signature pattern | Required |
| `TargetPattern` | string | Assertivo equivalent expression template | Required |
| `Exactness` | enum | `Exact` or `NonExact` | Required |
| `Authority` | enum | `AssertivoOfficial` or `RepoException` | Required |
| `Rationale` | string | Why the mapping is safe | Required |
| `Enabled` | bool | Whether rule is currently applied | Required |

### 3. DependencyRetentionDecision

Tracks per-project xUnit package retention/removal decisions.

| Field | Type | Description | Validation |
|---|---|---|---|
| `Project` | enum | Test project name | Required |
| `HasXunitNamespaceUsage` | bool | Whether xUnit namespace usage remains | Required |
| `Decision` | enum | `Keep` or `Remove` | Required |
| `CompileValidated` | bool | Project compiles after decision | Required when `Decision=Remove` |
| `TestsValidated` | bool | Project tests pass after decision | Required when `Decision=Remove` |
| `Notes` | string | Supporting evidence | Optional |

### 4. NonExactCandidateRecord

Represents one assertion intentionally not auto-converted.

| Field | Type | Description | Validation |
|---|---|---|---|
| `CandidateId` | string | Stable candidate ID | Required, unique |
| `FilePath` | string | Source file path | Required |
| `LineNumber` | integer | Candidate line number | `> 0` |
| `Method` | string | xUnit method name | Required |
| `Reason` | string | Why candidate is non-exact/ambiguous | Required |
| `SuggestedTarget` | string | Optional suggested Assertivo pattern | Optional |
| `ReviewStatus` | enum | `Pending`, `Accepted`, `Rejected`, `Deferred` | Required |

### 5. MigrationValidationRun

Captures staged validation evidence.

| Field | Type | Description | Validation |
|---|---|---|---|
| `RunId` | string | Validation run identifier | Required |
| `Scope` | enum | `ChangedProjects` or `FullRepository` | Required |
| `BuildPassed` | bool | Compile status | Required |
| `TestPassed` | bool | Test status | Required |
| `Timestamp` | datetime | Run time | Required |
| `CommandSet` | string[] | Commands executed | Required |

## Relationships

- `AssertionOccurrence` (many) -> `AssertionEquivalenceRule` (one optional by `RuleId`)
- `AssertionOccurrence` (many) -> `NonExactCandidateRecord` (zero or one, when not auto-converted)
- `DependencyRetentionDecision` (one per test project) depends on post-conversion `AssertionOccurrence` and namespace scans
- `MigrationValidationRun` validates both converted occurrences and dependency decisions

## State Transitions

### AssertionOccurrence lifecycle

`Discovered` -> `Converted` -> `Validated`

`Discovered` -> `Unchanged` -> `Validated`

Rules:
- Only `Classification=Exact` can transition to `Converted`.
- `Classification=NonExact|Unsupported|Ambiguous` must transition to `Unchanged` and create or link a candidate record.

### DependencyRetentionDecision lifecycle

`Keep` (default) -> `Remove` (allowed only if no xUnit namespace usage remains) -> `Validated`

Rules:
- `Remove` requires both `CompileValidated=true` and `TestsValidated=true`.
- Failed validation reverts decision to `Keep`.

## Baseline Inventory Snapshot

### xUnit assertion calls by method (current)

| Method | Count |
|---|---|
| `Single` | 13 |
| `Equal` | 8 |
| `Empty` | 6 |
| `Throws` | 3 |
| `All` | 2 |
| `Equivalent` | 2 |
| `Contains` | 1 |
| `ThrowsAsync` | 1 |
| `NotNull` | 1 |

### xUnit assertion calls by project (current)

| Project | Count |
|---|---|
| Contract | 15 |
| Integration | 11 |
| Unit | 11 |

### In-scope files containing xUnit assertions (current)

| File | Assert count |
|---|---|
| `tests/Messaggero.Tests.Contract/AckNackContractTests.cs` | 5 |
| `tests/Messaggero.Tests.Contract/BackpressureContractTests.cs` | 2 |
| `tests/Messaggero.Tests.Contract/LifecycleContractTests.cs` | 2 |
| `tests/Messaggero.Tests.Contract/PublishContractTests.cs` | 6 |
| `tests/Messaggero.Tests.Integration/AdapterIsolationTests.cs` | 2 |
| `tests/Messaggero.Tests.Integration/FanInIntegrationTests.cs` | 2 |
| `tests/Messaggero.Tests.Integration/FanOutFailureTests.cs` | 1 |
| `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs` | 6 |
| `tests/Messaggero.Tests.Unit/Configuration/ScopedHandlerValidationTests.cs` | 2 |
| `tests/Messaggero.Tests.Unit/Hosting/HandlerDispatcherTests.cs` | 2 |
| `tests/Messaggero.Tests.Unit/Hosting/MessageBusTests.cs` | 2 |
| `tests/Messaggero.Tests.Unit/Routing/RoutingTableTests.cs` | 5 |
