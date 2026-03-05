# Tasks: Multi-Transport Routing

**Input**: Design documents from `/specs/002-multi-transport-routing/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/public-api.md, quickstart.md

**Tests**: Included per constitution (Principle II: Test-First Development).

**Organization**: Tasks grouped by user story. US1/US2/US4 are P1 (core), US3 is P2 (type routing), US5 is P3 (observability).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1–US5)
- Exact file paths included

---

## Phase 1: Setup

**Purpose**: Create the Routing directory and new source files with empty/minimal implementations

- [X] T001 Create Routing directory structure at src/Messaggero/Routing/
- [X] T002 [P] Create PatternSpecificity enum in src/Messaggero/Routing/DestinationPattern.cs
- [X] T003 [P] Create TransportHealthEntry class in src/Messaggero.Abstractions/TransportHealthEntry.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core routing infrastructure that ALL user stories depend on. Must complete before any story begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Implement DestinationPattern compiled matcher (exact, single-wildcard *, multi-wildcard **) in src/Messaggero/Routing/DestinationPattern.cs
- [X] T005 [P] Write DestinationPatternTests covering exact match, single-wildcard, multi-wildcard, no-match, edge cases (empty, single-segment) in tests/Messaggero.Tests.Unit/DestinationPatternTests.cs
- [X] T006 Create RoutingRule class (TransportName, DestinationPattern?, MessageType?) in src/Messaggero/Routing/RoutingRule.cs
- [X] T007 Create ITransportRouter interface (ResolveTransport, Transports property) in src/Messaggero/Routing/ITransportRouter.cs
- [X] T008 Implement TransportRouter with destination-rule evaluation (specificity-ordered), default transport fallback, and startup validation (conflict detection, unregistered transport references) in src/Messaggero/Routing/TransportRouter.cs
- [X] T009 Write TransportRouterTests covering destination resolution, default fallback, no-match error, conflict detection at startup, unregistered transport validation in tests/Messaggero.Tests.Unit/TransportRouterTests.cs

**Checkpoint**: DestinationPattern and TransportRouter are fully tested in isolation. Routing infrastructure is ready for integration into the bus.

---

## Phase 3: User Story 1 — Register Multiple Transports (Priority: P1) 🎯 MVP

**Goal**: Multiple transports can be registered and connected within a single IMessageBus instance. Backward compatible with single-transport registration.

**Independent Test**: Configure both RabbitMQ and Kafka transports at startup; verify both connect and the bus starts successfully. Verify single-transport config still works unchanged.

### Tests for User Story 1

- [X] T010 [P] [US1] Write unit tests for MessageBusBuilder multi-transport registration (AddTransport, UseDefaultTransport, duplicate name detection, single-transport backward compat via UseTransport) in tests/Messaggero.Tests.Unit/MessageBusMultiTransportTests.cs

### Implementation for User Story 1

- [X] T011 [US1] Extend MessageBusBuilder with AddTransport(IMessageBusTransport), AddTransport(string, IMessageBusTransport), UseDefaultTransport(string) methods; update internal transport collection from single Transport property to Dictionary<string, IMessageBusTransport>; keep UseTransport for backward compat (registers by Name, sets default) in src/Messaggero/MessageBusBuilder.cs
- [X] T012 [US1] Update MessageBusBuilder.Validate() to build and validate TransportRouter (call TransportRouter constructor which validates conflicts and unregistered references) in src/Messaggero/MessageBusBuilder.cs
- [X] T013 [US1] Refactor MessageBus constructor to accept ITransportRouter instead of single IMessageBusTransport; connect all transports on startup with per-transport error handling (failed transports logged, bus starts with healthy ones); update DisposeAsync to disconnect all transports in src/Messaggero/MessageBus.cs
- [X] T014 [US1] Update ServiceCollectionExtensions.AddMessaggero to build TransportRouter from builder config and inject into MessageBus; register all transports for DI lifetime management in src/Messaggero/ServiceCollectionExtensions.cs
- [X] T015 [P] [US1] Add AddKafka(Action<KafkaConfiguration>) and AddKafka(string, Action<KafkaConfiguration>) extension methods to register Kafka as a named transport; keep existing UseKafka for backward compat in src/Messaggero.Transport.Kafka/ServiceCollectionExtensions.cs
- [X] T016 [P] [US1] Add AddRabbitMq(Action<RabbitMqConfiguration>) and AddRabbitMq(string, Action<RabbitMqConfiguration>) extension methods to register RabbitMQ as a named transport; keep existing UseRabbitMq for backward compat in src/Messaggero.Transport.RabbitMQ/ServiceCollectionExtensions.cs

**Checkpoint**: Multiple transports register and connect. Single-transport backward compatibility verified. Bus starts even if one transport fails to connect.

---

## Phase 4: User Story 2 — Publish Based on Destination (Priority: P1)

**Goal**: Publishing a message routes to the correct transport based on destination-pattern routing rules configured at startup.

**Independent Test**: Configure routing rules mapping "orders.*" → Kafka and "notifications.*" → RabbitMQ. Publish to each and verify correct transport is selected. Verify default fallback and error on unroutable destination.

### Tests for User Story 2

- [X] T017 [P] [US2] Write unit tests for MessageBus.PublishAsync multi-transport routing: destination rule matches correct transport, default transport fallback, unroutable destination throws, unavailable transport throws descriptive error in tests/Messaggero.Tests.Unit/MessageBusMultiTransportTests.cs

### Implementation for User Story 2

- [X] T018 [US2] Add RouteDestination(string destinationPattern, string transportName) method to MessageBusBuilder; collect destination routing rules in internal list in src/Messaggero/MessageBusBuilder.cs
- [X] T019 [US2] Update MessageBus.PublishAsync<T> to call _router.ResolveTransport(destination, typeof(T)) instead of using single _transport field; reject publish if resolved transport is not connected in src/Messaggero/MessageBus.cs

**Checkpoint**: Publishing routes to the correct transport based on destination patterns. Default and error paths work.

---

## Phase 5: User Story 4 — Subscribe Across Transports (Priority: P1)

**Goal**: Subscribing to a destination resolves the transport using the same routing rules as publishing, enabling simultaneous subscriptions on different transports.

**Independent Test**: Subscribe to a Kafka destination and a RabbitMQ destination in the same app. Publish to each and verify both handlers receive their messages. Verify one transport failure doesn't affect the other.

### Tests for User Story 4

- [X] T020 [P] [US4] Write unit tests for MessageBus.SubscribeAsync multi-transport routing: subscription created on correct transport per routing rules, independent subscriptions on different transports, failure isolation between transports in tests/Messaggero.Tests.Unit/MessageBusMultiTransportTests.cs

### Implementation for User Story 4

- [X] T021 [US4] Update MessageBus.SubscribeAsync<T> to call _router.ResolveTransport(destination, messageType: null) for destination-only resolution; create subscription on resolved transport in src/Messaggero/MessageBus.cs
- [X] T022 [US4] Ensure subscription disposal tracks which transport owns each subscription handle; update DisposeAsync to clean up per-transport subscriptions independently in src/Messaggero/MessageBus.cs

**Checkpoint**: Subscriptions work across multiple transports. Failure on one transport doesn't break subscriptions on others.

---

## Phase 6: User Story 3 — Publish Based on Message Type (Priority: P2)

**Goal**: Routing rules based on CLR message type select the correct transport, with class hierarchy walk (excluding interfaces) and destination-based rules taking precedence.

**Independent Test**: Configure type rules mapping OrderEvent → Kafka and SendEmailCommand → RabbitMQ. Publish each type and verify correct routing. Publish a derived type and verify it matches the base class rule.

### Tests for User Story 3

- [X] T023 [P] [US3] Write unit tests for TransportRouter type-based resolution: exact type match, base class hierarchy walk, most-derived-first ordering, destination-rule precedence over type-rule, cached type resolution in tests/Messaggero.Tests.Unit/TransportRouterTests.cs

### Implementation for User Story 3

- [X] T024 [US3] Add RouteType<T>(string transportName) and RouteType(Type, string) methods to MessageBusBuilder; collect type routing rules in internal list in src/Messaggero/MessageBusBuilder.cs
- [X] T025 [US3] Implement type-based resolution in TransportRouter: walk Type.BaseType chain (stop before object, ignore interfaces), check each level against type rules, cache results in ConcurrentDictionary<Type, string?> in src/Messaggero/Routing/TransportRouter.cs
- [X] T026 [US3] Add startup validation for type rules: same type mapped to different transports throws InvalidOperationException in src/Messaggero/Routing/TransportRouter.cs

**Checkpoint**: Type-based routing works. Destination rules correctly take precedence. Class hierarchy walk and caching verified.

---

## Phase 7: User Story 5 — Lifecycle and Health per Transport (Priority: P3)

**Goal**: Health checks report per-transport status with aggregate. Lifecycle events identify which transport they originate from.

**Independent Test**: Start both transports, query health check, verify per-transport entries. Simulate one transport disconnection and verify lifecycle event identifies the correct transport and aggregate health shows Degraded.

### Tests for User Story 5

- [X] T027 [P] [US5] Write unit tests for aggregate health check: all healthy → Healthy, mixed → Degraded, all unhealthy → Unhealthy, per-transport entries populated, backward compat with single transport in tests/Messaggero.Tests.Unit/MessageBusMultiTransportTests.cs
- [X] T028 [P] [US5] Write unit tests for lifecycle event forwarding from multiple transports: each event tagged with correct TransportName in tests/Messaggero.Tests.Unit/MessageBusMultiTransportTests.cs

### Implementation for User Story 5

- [X] T029 [US5] Add TransportEntries property (IReadOnlyList<TransportHealthEntry>) to HealthCheckResult in src/Messaggero.Abstractions/HealthCheckResult.cs
- [X] T030 [US5] Implement aggregate CheckHealthAsync in MessageBus: query all transports, build TransportHealthEntry per transport, compute aggregate status (all healthy → Healthy, mix → Degraded, all unhealthy → Unhealthy) in src/Messaggero/MessageBus.cs
- [X] T031 [US5] Update MessageBus lifecycle event forwarding: subscribe to OnLifecycleEvent on each transport and forward to bus-level listeners (events already contain TransportName) in src/Messaggero/MessageBus.cs

**Checkpoint**: Health checks return per-transport entries. Lifecycle events correctly identify originating transport. Aggregate health reflects partial outages.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Integration tests, backward compatibility validation, and cleanup

- [X] T032 [P] Write contract tests for updated MessageBusBuilder API (AddTransport, RouteDestination, RouteType, UseDefaultTransport) verifying builder validation rules in tests/Messaggero.Tests.Contract/TransportContractTests.cs
- [X] T033 Write multi-transport integration test: configure RabbitMQ + Kafka with destination routing, publish to both, subscribe on both, verify end-to-end message delivery across transports in tests/Messaggero.Tests.Integration/MultiTransportIntegrationTests.cs
- [X] T034 Write backward compatibility integration test: verify existing single-transport test scenarios pass without modification in tests/Messaggero.Tests.Integration/MultiTransportIntegrationTests.cs
- [X] T035 Run quickstart.md validation: verify all code samples from specs/002-multi-transport-routing/quickstart.md compile and execute correctly
- [X] T036 Verify all existing unit and integration tests pass with no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1 — Register)**: Depends on Phase 2 — BLOCKS US2, US4
- **Phase 4 (US2 — Publish Dest)**: Depends on Phase 3
- **Phase 5 (US4 — Subscribe)**: Depends on Phase 3 — can run in parallel with Phase 4
- **Phase 6 (US3 — Publish Type)**: Depends on Phase 4 (needs RouteDestination precedence to exist)
- **Phase 7 (US5 — Health/Lifecycle)**: Depends on Phase 3 — can run in parallel with Phases 4–6
- **Phase 8 (Polish)**: Depends on all previous phases

### User Story Dependencies

- **US1 (Register)**: Foundation only — no story dependencies
- **US2 (Publish Dest)**: Depends on US1 (transports must be registered before routing)
- **US4 (Subscribe)**: Depends on US1 — independent of US2
- **US3 (Publish Type)**: Depends on US2 (destination precedence over type per FR-004)
- **US5 (Health/Lifecycle)**: Depends on US1 — independent of US2/US3/US4

### Parallel Opportunities

Within each phase, tasks marked [P] can run in parallel:

- **Phase 1**: T002 ∥ T003
- **Phase 2**: T005 can start in parallel with T004 (test-first)
- **Phase 3**: T015 ∥ T016 (Kafka and RabbitMQ extensions)
- **Phase 4 ∥ Phase 5**: US2 and US4 can run in parallel after US1
- **Phase 5 ∥ Phase 7**: US4 and US5 can run in parallel
- **Phase 8**: T032 ∥ T033 ∥ T034

---

## Parallel Example: After Phase 3 (US1 Complete)

```
Stream A (Publish):          Stream B (Subscribe):       Stream C (Health):
T017 (publish tests)         T020 (subscribe tests)      T027 ∥ T028 (health tests)
T018 (RouteDestination)      T021 (subscribe routing)    T029 (TransportHealthEntry)
T019 (publish routing)       T022 (subscription cleanup) T030 (aggregate health)
                                                         T031 (lifecycle forwarding)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (routing infrastructure)
3. Complete Phase 3: User Story 1 (register multiple transports)
4. **STOP and VALIDATE**: Both transports connect, single-transport still works
5. Deploy if ready — multi-transport registration is usable even without routing

### Incremental Delivery

1. Setup + Foundational → Routing infrastructure ready
2. Add US1 (Register) → Transports coexist → **MVP**
3. Add US2 (Publish Dest) → Destination-based routing works → Deploy/Demo
4. Add US4 (Subscribe) → Subscriptions span transports → Deploy/Demo
5. Add US3 (Publish Type) → Type-based routing adds convenience → Deploy/Demo
6. Add US5 (Health) → Per-transport observability → Production-ready
7. Polish → Integration tests, backward compat validation → Release

---

## Notes

- [P] tasks = different files, no dependencies on incomplete same-phase tasks
- [Story] label maps to spec.md user stories (US1–US5)
- Constitution requires test-first: write tests before implementation within each story
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All routing types are internal (not exposed to library consumers)
- MessageBus constructor change (single transport → router) is the biggest refactor — handle in T013
