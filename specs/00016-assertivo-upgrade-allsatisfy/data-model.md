# Data Model: Assertivo 0.3.0 Upgrade and Assert.All Conversion

**Feature**: `00016-assertivo-upgrade-allsatisfy`  
**Phase**: 1 — Design

## Not Applicable

This feature is a dependency version bump and a mechanical test assertion conversion.
It introduces no new domain entities, no new data shapes, and no new persistent state.

There are no entities, value objects, aggregates, state machines, or validation rules
to model.

## Change Surface Summary

For implementation reference, the complete set of source locations affected is:

| File | Change Type | Detail |
|------|-------------|--------|
| `tests/Messaggero.Tests.Unit/Messaggero.Tests.Unit.csproj` | Version bump | `Assertivo` `0.2.0` → `0.3.0` |
| `tests/Messaggero.Tests.Contract/Messaggero.Tests.Contract.csproj` | Version bump | `Assertivo` `0.2.0` → `0.3.0` |
| `tests/Messaggero.Tests.Integration/Messaggero.Tests.Integration.csproj` | Version bump | `Assertivo` `0.2.0` → `0.3.0` |
| `tests/Messaggero.Tests.Integration/RoutingIntegrationTests.cs` | Assertion conversion | 2 `Assert.All` → `AllSatisfy` call sites |
| `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md` | Artifact update | `NC-0001`, `NC-0002` → `Resolved` |
