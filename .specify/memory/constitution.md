<!--
  Sync Impact Report
  ==================
  Version change: N/A → 1.0.0 (initial adoption)
  Modified principles: N/A (first version)
  Added sections:
    - Core Principles (4 principles: Code Quality, Testing Standards,
      UX Consistency, High Performance & Throughput)
    - Performance Standards
    - Development Workflow
    - Governance
  Removed sections: N/A
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ no update needed
      (Constitution Check is dynamic; plan references constitution
      generically)
    - .specify/templates/spec-template.md ✅ no update needed
      (spec template is principle-agnostic)
    - .specify/templates/tasks-template.md ✅ no update needed
      (task categorization is generated at runtime from principles)
    - .github/agents/*.md ✅ no update needed
      (agents reference constitution by path, no hardcoded
      principle names)
  Follow-up TODOs: none
-->

# Messaggero Constitution

## Core Principles

### I. Code Quality (NON-NEGOTIABLE)

All production code MUST meet the following quality standards:

- **Readability**: Every module, public function, and data type MUST
  have a clear, single responsibility. Names MUST convey intent
  without requiring comments to explain "what."
- **Consistency**: A single code style MUST be enforced project-wide
  via automated formatters and linters. No PR may be merged with
  unresolved lint violations.
- **Static Analysis**: The codebase MUST pass all configured static
  analysis checks with zero warnings. New warnings introduced by a
  change MUST be resolved before merge.
- **Minimal Complexity**: Cyclomatic complexity per function MUST NOT
  exceed 10. Functions exceeding this threshold MUST be refactored
  or receive an explicit, documented justification in a complexity
  tracking table.
- **No Dead Code**: Unused imports, unreachable branches, and
  commented-out code MUST be removed. Feature flags are the only
  acceptable mechanism for dormant functionality.
- **Documentation**: Public APIs MUST have doc-comments describing
  purpose, parameters, return values, and error conditions.

**Rationale**: A messaging platform's longevity depends on a codebase
that any team member can navigate, modify, and extend with
confidence. Automated enforcement removes subjective debate.

### II. Testing Standards (NON-NEGOTIABLE)

Every feature MUST be accompanied by tests that prove correctness
before and after merge:

- **Test-First Development**: Tests MUST be written before or
  alongside implementation. A feature is not considered started
  until at least one failing test exists for it.
- **Coverage Floors**: Line coverage MUST NOT drop below 80% on any
  merge. Critical paths (authentication, message delivery,
  payment/billing if applicable) MUST maintain ≥ 95% coverage.
- **Test Pyramid**: The project MUST maintain a healthy distribution:
  unit tests (majority), integration tests (key workflows), and
  end-to-end tests (critical user journeys only).
- **Determinism**: Every test MUST be deterministic. Flaky tests MUST
  be quarantined within 24 hours and fixed or removed within one
  sprint.
- **Contract Tests**: Any change to a public API or inter-service
  contract MUST include updated contract tests verifying backward
  compatibility or an explicit breaking-change notice.
- **Performance Regression Tests**: Latency-sensitive paths MUST have
  benchmark tests that fail when p95 regresses beyond a defined
  threshold (see Principle IV).

**Rationale**: Messaging systems handle user trust and real-time data.
Untested code is unshippable code; regressions erode reliability
and user confidence.

### III. User Experience Consistency

All user-facing surfaces MUST deliver a coherent, predictable
experience:

- **Design System Adherence**: Every UI component MUST use the
  project's shared design tokens (colors, typography, spacing,
  motion). Ad-hoc styling is prohibited.
- **Interaction Patterns**: Common actions (send, reply, search,
  navigate) MUST behave identically across all supported platforms
  and form factors. Platform-specific adaptations are permitted only
  where OS conventions demand them, and MUST be documented.
- **Accessibility**: All interactive elements MUST meet WCAG 2.1 AA.
  Screen-reader labels, keyboard navigation, and sufficient color
  contrast are mandatory, not optional.
- **Error Communication**: User-facing errors MUST be actionable and
  written in plain language. Technical details (stack traces, error
  codes) MUST be logged but MUST NOT be shown to end users.
- **Loading & Empty States**: Every view that loads data MUST have
  explicit loading, empty, and error states. Skeleton screens are
  preferred over spinners for content-heavy views.
- **Offline Resilience**: The application MUST degrade gracefully
  when connectivity is lost. Queued messages MUST be delivered
  automatically on reconnection with no user intervention.

**Rationale**: Messaggero competes on trustworthiness and ease of
use. Inconsistent or inaccessible UX drives users to alternatives
faster than missing features do.

### IV. High Performance & Throughput (NON-NEGOTIABLE)

The system MUST be engineered for low latency and high throughput at
every layer:

- **Latency Budgets**: End-to-end message delivery MUST complete
  within 200 ms at p95 under normal load. API endpoints MUST
  respond within 100 ms at p95 for read operations and 250 ms for
  writes.
- **Throughput Targets**: The system MUST sustain at least 10,000
  messages per second per node without degradation. Capacity
  planning MUST demonstrate horizontal scalability.
- **Resource Efficiency**: Memory allocations on hot paths MUST be
  minimized. Object pooling or arena allocation MUST be used where
  profiling shows allocation pressure. GC-pause-sensitive paths
  MUST be profiled and optimized.
- **Concurrency**: I/O-bound operations MUST use asynchronous,
  non-blocking patterns. Thread/goroutine/task pools MUST be sized
  based on measured workload, not hard-coded defaults.
- **Observability**: All services MUST emit structured logs, metrics
  (latency histograms, throughput counters, error rates), and
  distributed traces. Dashboards MUST exist for every latency
  budget defined above.
- **Regression Prevention**: Benchmark suites MUST run in CI. Any
  commit that degrades p95 latency by more than 10% or throughput
  by more than 5% MUST fail the pipeline and require explicit
  sign-off to override.

**Rationale**: A messaging platform's core value proposition is
instant, reliable delivery. Performance is a feature, not an
afterthought; users perceive latency as broken functionality.

## Performance Standards

Quantitative targets that MUST be validated continuously:

| Metric | Target | Measurement Method |
|---|---|---|
| Message delivery p95 | ≤ 200 ms | End-to-end trace |
| API read p95 | ≤ 100 ms | Server-side histogram |
| API write p95 | ≤ 250 ms | Server-side histogram |
| Throughput per node | ≥ 10,000 msg/s | Load test (sustained) |
| Cold start time | ≤ 3 s | Instrumented boot |
| UI time-to-interactive | ≤ 2 s on 4G | Lighthouse / WebPageTest |
| Test suite duration | ≤ 5 min (unit) | CI pipeline timer |

Performance budgets MUST be reviewed quarterly. Any budget change
MUST follow the amendment process in the Governance section.

## Development Workflow

All contributors MUST follow these workflow gates:

1. **Branch Strategy**: One branch per feature or fix, branched from
   the main integration branch. Branch names MUST follow the
   pattern `<issue-number>-<short-description>`.
2. **Pre-Push Checks**: Developers MUST run linters, formatters, and
   the unit test suite locally before pushing. A pre-commit/pre-push
   hook SHOULD automate this.
3. **Pull Request Requirements**:
   - At least one approving review from a code owner.
   - All CI checks (lint, test, benchmark, coverage) MUST pass.
   - PR description MUST reference the spec or issue being
     addressed.
4. **Merge Policy**: Squash-merge is the default. Merge commits are
   permitted only for long-lived integration branches.
5. **Continuous Integration**: The CI pipeline MUST execute, in
   order: lint → build → unit tests → integration tests → benchmark
   regression check → coverage report.
6. **Release Cadence**: Releases follow semantic versioning.
   Breaking changes MUST be documented in a changelog and
   communicated at least one release cycle in advance.

## Governance

This constitution is the supreme authority for all Messaggero
development decisions. In any conflict between this document and
other guidance, this document prevails.

- **Amendment Procedure**: Any contributor may propose an amendment
  via a pull request modifying this file. The PR MUST include a
  rationale section and a Sync Impact Report. Approval requires
  sign-off from at least two code owners.
- **Versioning Policy**: This constitution follows semantic
  versioning: MAJOR for principle removals or incompatible
  redefinitions, MINOR for new principles or material expansions,
  PATCH for clarifications and wording fixes.
- **Compliance Review**: Every pull request MUST be evaluated against
  applicable principles. The plan template's "Constitution Check"
  section captures gate compliance at planning time; the CI
  pipeline enforces automated checks at merge time.
- **Dispute Resolution**: If a principle is ambiguous in a specific
  context, the dispute MUST be raised as an issue, discussed, and
  resolved via a PATCH amendment before the ambiguous work
  proceeds.

**Version**: 1.0.0 | **Ratified**: 2026-03-04 | **Last Amended**: 2026-03-04
