# Quickstart: Modernize xUnit Assertions with Assertivo

**Feature**: 00014-xunit-assertivo-migration
**Date**: 2026-05-01

## Goal

Replace supported exact xUnit assertions in test source files with Assertivo equivalents while preserving behavior and producing review artifacts for non-exact candidates.

## Prerequisites

- .NET 10 SDK installed
- Repository restored successfully
- Work performed on branch `00014-xunit-assertivo-migration`

## Step 1: Establish Baseline

Run full baseline compile and tests before making edits.

```powershell
dotnet restore Messaggero.slnx
dotnet build Messaggero.slnx
dotnet test Messaggero.slnx
```

## Step 2: Inventory xUnit Assertion Occurrences

Generate baseline inventory from test source.

```powershell
$matches = Get-ChildItem tests -Recurse -Filter *.cs | Select-String -Pattern '\bAssert\.'
$matches | Select-Object Path, LineNumber, Line | Format-Table -AutoSize
```

Expected baseline for this feature:
- 12 files with xUnit assertion calls
- 37 total `Assert.*` calls

## Step 3: Apply Exact 1:1 Conversions Only

Convert only approved exact mappings.

| xUnit source | Assertivo target |
|---|---|
| `Assert.Equal(expected, actual)` | `actual.Should().Be(expected)` |
| `Assert.Single(collection)` | `collection.Should().ContainSingle()` |
| `Assert.Empty(collection)` | `collection.Should().BeEmpty()` |
| `Assert.NotNull(value)` | `value.Should().NotBeNull()` |
| `Assert.Contains(item, collection)` | `collection.Should().Contain(item)` |
| `Assert.Throws<T>(action)` | `action.Should().Throw<T>()` |
| `Assert.ThrowsAsync<T>(asyncAction)` | `asyncAction.Should().ThrowAsync<T>()` |
| `Assert.Equivalent(expected, actual)` | `actual.Should().BeEquivalentTo(expected)` |

Rules:
- Do not auto-convert `Assert.All(...)` in this feature pass.
- Leave unsupported/ambiguous patterns unchanged.
- Keep test method names, structure, and data unchanged.

## Step 4: Update Imports Safely

For modified files:
- Ensure `using Assertivo;` is present when fluent assertions are used.
- Keep `using Xunit;` as long as any xUnit usage remains (attributes/framework/assertions).
- Remove imports only when proven unused in the file.

## Step 5: Produce Non-Exact Review Artifacts

For every non-exact candidate discovered, generate both artifacts:
- Machine-readable: `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json`
- Human-readable: `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md`

```powershell
& .\specs\00014-xunit-assertivo-migration\artifacts\scripts\scan-assertions.ps1 -RootPath . -OutputPath specs/00014-xunit-assertivo-migration/artifacts/assertion-occurrences.json
& .\specs\00014-xunit-assertivo-migration\artifacts\scripts\generate-non-exact-artifacts.ps1 -RootPath . -InputPath specs/00014-xunit-assertivo-migration/artifacts/assertion-occurrences.json -JsonOutputPath specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json -MarkdownOutputPath specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md
```

Minimum required fields per candidate:
- File path
- Line number
- Original assertion snippet
- Reason for non-exact classification
- Suggested target pattern (if any)

## Step 6: Staged Validation Per Change Set

After each change set, validate only touched projects first.

```powershell
# Example for Unit project changes
dotnet build tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj
dotnet test tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj
```

Apply same pattern for `Contract` and/or `Integration` when touched.

## Step 7: Final Completion Gate

Before declaring migration complete, run full repository validation.

```powershell
dotnet build Messaggero.slnx
dotnet test Messaggero.slnx
```

Expected outcome:
- Compile passes
- Full test pass rate matches baseline
- Non-exact candidates are documented in both artifact formats

## Step 8: Optional xUnit Dependency Removal Check

Only if considering dependency removal in a test project:
1. Verify no xUnit namespace usage remains in that project.
2. Remove dependency from that project only.
3. Re-run project build + tests.
4. Keep dependency if either check fails.

Default behavior remains to keep xUnit dependencies.

## Execution Notes (Current Run)

- Staged validation evidence is recorded in:
	- `specs/00014-xunit-assertivo-migration/artifacts/us1-validation.md`
	- `specs/00014-xunit-assertivo-migration/artifacts/us2-validation.md`
- Final completion gate evidence is recorded in:
	- `specs/00014-xunit-assertivo-migration/artifacts/final-validation.md`
- Non-exact consistency evidence is recorded in:
	- `specs/00014-xunit-assertivo-migration/artifacts/non-exact-artifact-consistency.md`
- Scope audit now passes after resolving the out-of-scope workspace change:
	- `specs/00014-xunit-assertivo-migration/artifacts/scope-audit.md`
