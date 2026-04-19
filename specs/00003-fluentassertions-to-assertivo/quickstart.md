# Quickstart: Replace FluentAssertions with Assertivo

**Feature**: 00003-fluentassertions-to-assertivo
**Date**: 2026-04-18

## Migration Steps

### Step 1: Update Package References

In each of the three test `.csproj` files, replace:

```diff
- <PackageReference Include="FluentAssertions" Version="8.3.0" />
+ <PackageReference Include="Assertivo" Version="0.1.2" />
```

**Files**:
- `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj`
- `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj`
- `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj`

### Step 2: Update Using Directives

In all 18 test `.cs` files, replace:

```diff
- using FluentAssertions;
+ using Assertivo;
```

### Step 3: Restore Packages

```shell
dotnet restore
```

### Step 4: Build and Verify Compilation

```shell
dotnet build
```

All projects must compile with zero errors and zero new warnings (`TreatWarningsAsErrors` is enabled).

### Step 5: Run Full Test Suite

```shell
dotnet test
```

All previously passing tests must continue to pass.

### Step 6: Verify No Remaining References

```shell
dotnet list package | Select-String "FluentAssertions"
```

Expected output: no matches.

## Fallback Procedure

If any assertion pattern fails to compile after the swap:

1. Identify the specific method call that is unsupported
2. Rewrite using native xUnit `Assert.*` methods
3. Do not block the migration waiting for upstream Assertivo support
