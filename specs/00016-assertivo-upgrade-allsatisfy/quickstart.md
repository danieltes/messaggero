# Quickstart: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Feature**: `00016-assertivo-upgrade-allsatisfy`  
**Branch**: `00016-assertivo-upgrade-allsatisfy`

## Overview

This feature has two sequential steps:

1. **Bump Assertivo** from `0.2.0` to `0.3.0` in all three test projects and verify the build.
2. **Convert two `Assert.All` call sites** in `RoutingIntegrationTests.cs` to `AllSatisfy` and verify the tests.

Step 2 depends on Step 1 (the `AllSatisfy` method only exists in 0.3.0).

---

## Step 1 — Bump Assertivo to 0.3.0

Edit the `PackageReference` version in each of the three test project files:

**`tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj`** — line 19:
```xml
<!-- Before -->
<PackageReference Include="Assertivo" Version="0.2.0" />
<!-- After -->
<PackageReference Include="Assertivo" Version="0.3.0" />
```

**`tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj`** — line 17:
```xml
<!-- Before -->
<PackageReference Include="Assertivo" Version="0.2.0" />
<!-- After -->
<PackageReference Include="Assertivo" Version="0.3.0" />
```

**`tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`** — line 18:
```xml
<!-- Before -->
<PackageReference Include="Assertivo" Version="0.2.0" />
<!-- After -->
<PackageReference Include="Assertivo" Version="0.3.0" />
```

### Verify build

```bash
dotnet build Messaggero.slnx
```

Expected: exit code 0, zero warnings.

---

## Step 2 — Convert Assert.All to AllSatisfy

Edit `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs`, lines 80–81:

```csharp
// Before
Assert.All(kafkaAdapter.PublishedMessages, m => m.Type.Should().Be("OrderPlaced"));
Assert.All(rabbitAdapter.PublishedMessages, m => m.Type.Should().Be("EmailRequested"));

// After
kafkaAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("OrderPlaced"));
rabbitAdapter.PublishedMessages.Should().AllSatisfy(m => m.Type.Should().Be("EmailRequested"));
```

> **Important**: The `using Xunit;` directive at line 10 must be retained —
> `[Fact]` attributes still depend on it.

### Verify tests

```bash
dotnet test Messaggero.slnx
```

Expected: all tests green.

### Verify zero Assert. occurrences

```powershell
Select-String -Path "tests/**/*.cs" -Pattern "Assert\." -Recurse
```

Expected: no matches.

---

## Step 3 — Update 00014 Non-Exact Candidate Artifacts

In `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md`, update
the `Review Status` column for `NC-0001` and `NC-0002` from `Pending` to `Resolved`.

---

## Success Checklist

- [ ] SC-001: All three `.csproj` files reference `Assertivo 0.3.0`
- [ ] SC-002: `dotnet build` exits 0 with zero warnings
- [ ] SC-003: `dotnet test` green across Unit, Contract, Integration
- [ ] SC-004: Zero `Assert.` occurrences in test `.cs` files
- [ ] SC-005: NC-0001 and NC-0002 marked `Resolved` in 00014 artifacts
