# Contract: Non-Exact Review Artifacts

**Feature**: 00014-xunit-assertivo-migration
**Date**: 2026-05-01

This contract defines the required outputs for non-exact assertion mapping candidates.

## 1. Required Artifacts

When at least one non-exact candidate exists, generate both artifacts:

1. Machine-readable artifact
- Path: `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.json`

2. Human-readable artifact
- Path: `specs/00014-xunit-assertivo-migration/artifacts/non-exact-candidates.md`

## 2. Machine-Readable Schema

The JSON artifact must be an array of candidate objects.

```json
[
  {
    "candidateId": "string",
    "project": "Unit|Integration|Contract",
    "filePath": "string",
    "lineNumber": 1,
    "assertMethod": "string",
    "sourceSnippet": "string",
    "classification": "NonExact|Ambiguous|Unsupported",
    "reason": "string",
    "suggestedTarget": "string|null",
    "reviewStatus": "Pending|Accepted|Rejected|Deferred"
  }
]
```

Validation rules:
- `candidateId` must be unique per artifact.
- `filePath` must reference a file under `tests/`.
- `lineNumber` must be positive.
- `reason` must be non-empty.
- `reviewStatus` must default to `Pending` when created.

## 3. Human-Readable Summary Format

The Markdown artifact must include:

1. Header with feature ID and generation timestamp.
2. Summary counts by project and by assertion method.
3. Candidate table with at least:
- ID
- File
- Line
- Original assertion
- Reason
- Suggested target
- Review status
4. Notes section for reviewer decisions.

## 4. Consistency Rules Between Artifacts

1. Candidate count must match between JSON and Markdown.
2. Candidate IDs must match exactly between both artifacts.
3. Each Markdown row must map to one JSON record.
4. If artifacts are regenerated, stale candidate IDs not in current scan must be removed.

## 5. Acceptance Conditions

SC-009 is satisfied only when:

1. Both artifacts are generated for runs with non-exact candidates.
2. Artifacts are internally consistent by candidate count and IDs.
3. Non-exact candidates remain unconverted in source files.
