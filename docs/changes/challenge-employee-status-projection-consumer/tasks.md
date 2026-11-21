# Tasks: Challenge Employee Status Projection Consumer

## SDD and contract review

- [x] Fetch the live `feature/backend-foundation` HEAD and reconcile the existing Challenge implementation and CI evidence.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, communication conventions, ADR-0001/0002 and shared event/projection contracts.
- [x] Correct the proposal/design context: Challenge projects, persistence, consumer, topology and tests already exist on the live branch.
- [x] Preserve projection-only scope; do not invent Challenge programme/participation rules.
- [x] Keep inactive-employee historical-read versus mutation policy as open question 19.
- [x] Map acceptance criteria to implementation and verification.
- [x] Incorporate the 14 owner-selected decisions recorded in the SDD review (minimal projection; initial permanent inbox retention; tolerant additive JSON; exact source; invalid-event DLQ; separate health; strict version ordering; DB atomicity; conflicting duplicate integrity detection; SHA-256; fingerprint field exclusions; RFC 8785 JCS; shared DLQ classifications; bounded exponential full-jitter retry).
- [x] Owner explicitly approved this revised proposal/spec/design/tasks package on 2026-11-20; implementation reconciliation is authorized.

## Existing implementation already present on the live branch

- [x] Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references.
- [x] Challenge.UnitTests registered in backend/TeamPulse.sln.
- [x] Challenge-owned MySQL database initialization/grant and connection configuration.
- [x] Minimal employee status projection, inbox entity and transport-independent application processor.
- [x] Atomic inbox + conditional lifecycle-version upsert in Challenge-owned MySQL.
- [x] Checked-in EF Core/Pomelo migration and model snapshot; startup migration.
- [x] Separate liveness/readiness and consumer-health endpoints.
- [x] Challenge RabbitMQ consumer with manual ack after processor completion, prefetch 1, bounded transient retry and reconnect.
- [x] Challenge-owned quorum queue, DLX and DLQ without changing Habit or other service queues.
- [x] Consumer disabled by default and configuration validated when enabled.
- [x] Startup-reconnect test and local README/communication documentation; CI #105 passed.

## Reconcile implementation against approved SDD

- [x] Validate actorEmployeeId UUID, exact case-sensitive `hr-admin-api` source, tolerant unknown properties and all required schema-v1 fields.
- [x] Add normalized RFC 8785 JCS + SHA-256 fingerprint and algorithm identifier to inbox persistence.
- [x] Compare fingerprints on duplicate event IDs; distinguish matching Duplicate from conflicting DuplicateEventIntegrityViolation without mutating the original inbox/projection.
- [x] Ensure fingerprint excludes optional note and unknown additive properties and includes the complete recognized normalized envelope fields.
- [x] Implement explicit classified publish-confirm handling for invalid events, unsupported schema versions, invalid producers and duplicate-event integrity violations; source acknowledgement follows successful mandatory publish confirmation.
- [x] Verify permanent invalid/integrity events reach the shared lifecycle DLQ and original delivery is acknowledged only after confirmed dead-letter handling in RabbitMQ Testcontainers CI #107.
- [x] Add deterministic bounded exponential full-jitter delay policy tests and explicit `TransientRetryExhausted` / `NonTransientProcessingFailure` DLQ classifications.
- [x] Verify transient exhaustion and non-transient processing failures reach the shared lifecycle DLQ, and failed DLQ publication leaves the original delivery unacknowledged, in RabbitMQ Testcontainers CI #108 (40 Challenge tests; full run 164/164 passed).
- [ ] Verify inbox has no TTL/automatic cleanup and stores no raw payload or note.
- [ ] Align health response states with the separate liveness, MySQL readiness and RabbitMQ consumer-health contract.


- [x] Diagnose CI #120 reconnect failure: the last failure stage was `connection` with `BrokerUnreachableException -> AggregateException -> ConnectFailureException -> SocketException`; the source queue retained one message and had zero consumers. The Challenge Testcontainers fixture used a dynamically mapped host port, while the consumer retained its original URI across container restart.
- [ ] Stabilize the Challenge Testcontainers AMQP host port across stop/start and verify the URI remains unchanged; rerun the complete broker-loss replay and sentinel/prefetch acceptance test. Keep CEPS-11/CEPS-14 open until that end-to-end test passes.

## Verification gaps

- [ ] MySQL 8.4 Testcontainers: clean and repeated host-startup migration, exact schema/model assertions and projection reader.
- [ ] MySQL 8.4 Testcontainers: same event ID/same fingerprint and same event ID/different fingerprint.
- [ ] MySQL 8.4 Testcontainers: duplicate-key race safely reads the committed fingerprint after rolling back the losing insert.
- [ ] MySQL 8.4 Testcontainers: concurrent N/N+1 processing leaves maximum lifecycle version; rollback leaves neither inbox nor projection mutation.
- [ ] RabbitMQ Testcontainers: duplicate redelivery after commit-before-ack and idempotent replay (CI #111 still timed out waiting for reconnect after callback cancellation; the consumer now tracks and drains the active callback before session disposal, pending rerun).
- [ ] RabbitMQ Testcontainers: invalid event and integrity violation DLQ handling, including DLX failure and redelivery.
- [ ] RabbitMQ Testcontainers: bounded exponential retry, exhaustion/non-transient classifications and failed-DLQ redelivery for retry exhaustion.
- [x] RabbitMQ Testcontainers: broker unavailable at startup and reconnect; startup-reconnect test passed in CI #105.
- [ ] RabbitMQ Testcontainers: established-session loss/reconnect and duplicate redelivery after commit-before-ack (CI #118 failed at the direct DuplicateReplayObserved wait after restart; 44/45 Challenge, 24 Habit and 100 Identity tests passed. The previous-session cancellation and completed-disposal assertions passed, but duplicate replay did not arrive within 30 seconds. CEPS-11/CEPS-14 remain open pending successful CI).
- [x] Add monotonic in-memory connection-attempt and established-session counters and include them, plus source queue message/consumer counts, in bounded replay-test timeout diagnostics. These counters do not change the public health contract; end-to-end replay remains pending CI.
- [x] Add in-memory session-setup stage and last-failure exception-type-chain diagnostics so reconnect failures can be localized to connection, channel creation, QoS or consumer registration. Store no exception messages, broker URI or credentials; verify this with a unit test.
- [x] Bound RabbitMQ connection-attempt, AMQP handshake and protocol continuation timeouts through a validated consumer option; deterministic tests reject out-of-range values. End-to-end reconnect verification remains pending CI.
- [x] Add a completed-session counter that advances after RabbitMQ channels/connection finish disposal; the established-session replay test waits for this signal before restarting the broker. End-to-end verification remains pending CI.
- [ ] RabbitMQ Testcontainers: restore the Challenge DLQ binding after a mandatory publish failure and verify the original unacknowledged delivery is processed and dead-lettered after reconnect.
- [ ] Host health integration tests prove DB readiness is independent of RabbitMQ and consumer health is separately observable.
- [ ] Privacy/logging tests prove no event body, note, credentials or connection string is logged/persisted.
- [x] CI #103 passed: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 10 Python tests and 144 .NET tests.
- [x] CI #104 passed: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 10 Python tests and 146 .NET tests.
- [x] CI #105 passed for `787bf97b1fcd4083eb6c59bc0595363145e3c83e`: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 11 Python tests and 147 .NET tests (23 Challenge, 24 Habit, 100 Identity).
- [x] CI #106 passed for `98595993774edfd8e6b83028f217dcf3d72377b8`: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 11 Python tests and 151 .NET tests (27 Challenge, 24 Habit, 100 Identity), 0 failed/skipped. [CI run #106](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44792610342).
- [x] CI #107 passed for `24c13c8c5fc72c4c041be1d862cf203662adddf9`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology, 11 Python topology tests and 155 .NET tests (31 Challenge, 24 Habit, 100 Identity), 0 failed/skipped. [CI run #107](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44795560333).
- [x] CI #108 passed for `3702c9a04186d304ca8b6ae0e5244cdfe262a105`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology, 11 Python topology tests and 164 .NET tests (40 Challenge, 24 Habit, 100 Identity), 0 failed/skipped. [CI run #108](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44805490633).
- [ ] CI #117 for `cc5be875c398c5bf67de82a094a7b2af24494edc` failed: Release build passed with 0 warnings/errors; Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed; .NET tests were 168/169 (44/45 Challenge, 24 Habit, 100 Identity). The sole failure was `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck`, timing out at the post-restart `status.IsConnected` wait (test line 201). [CI run #117](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44857671950).
- [ ] CI #118 for `6ffd928966659f228851f95cc1137d8f6bb6e145` failed: Release build passed with 0 warnings/errors; Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed; .NET tests were 168/169 (44/45 Challenge, 24 Habit, 100 Identity), with zero skipped. Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck timed out at line 204 waiting for DuplicateReplayObserved after broker restart. The previous session cancellation and completed-disposal assertions passed. [CI run #118](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44862518699).
- [ ] CI #112 for `acd34c9fa35fa34c77be4bcdfd8aac150e2c98fd` failed: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology and all 11 Python tests passed; .NET tests had 162/165 pass (38/41 Challenge, 24 Habit, 100 Identity). Two failed-DLQ-publish tests timed out waiting for consumer disconnection; commit-before-ack replay timed out waiting for reconnect after broker restart. [CI run #112](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44831788475).
- [ ] CI #113 for intermediate commit `18d1cdeb068c26baab1044cc0742c993a69a6224` was cancelled after the follow-up correction landed; its build had reported CS1002/CS1056 in the new DLQ-recovery test's escaped JSON string. The literal was corrected in `6cadec8c2f9bd44c5ec1553c286ee847b9845762`. [CI run #113](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44838462993).
- [ ] CI #114 for `6cadec8c2f9bd44c5ec1553c286ee847b9845762` failed: restore and Release build passed (0 warnings/errors), Compose validation, RabbitMQ startup/topology and all 11 Python tests passed; .NET tests were 164/165 (41/42 Challenge, 24 Habit, 100 Identity), with `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck` timing out while waiting for the consumer to reconnect after broker restart. [CI run #114](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44838518946).
- [ ] CI #115 for `caaa8108c330e004e0446775d626246a46817b85` failed: Restore and Release build passed (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed; .NET tests were 167/168 (43/44 Challenge, 24 Habit, 100 Identity). The sole failure was `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck`, timing out at line 182 while waiting for `status.IsConnected` after broker restart. The configured 5-second connection/handshake timeout did not resolve the reconnect wait. [CI run #115](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44845945644).
- [ ] CI #116 for `9382d389835fb505f3b9e3c5782ded76db4a299a` failed: Release build passed with 0 warnings/errors, Docker Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed; .NET tests were 167/169 (43/45 Challenge, 24 Habit, 100 Identity). `Consumer_LeavesOriginalUnacknowledgedWhenRetryExhaustionDeadLetterCannotBeConfirmed` read the source queue before the old channel had completed disposal/requeue (test line 329); `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck` timed out at the post-restart `IsConnected` wait (test line 198). [CI run #116](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44851451284).
- [ ] CI #110 for `5dc66ddb65ce5b9479c85c4e93ff93c85d59c033` failed: 164/165 .NET tests passed (40/41 Challenge, 24 Habit, 100 Identity); the commit-before-ack broker-stop/restart test timed out waiting for the consumer to reconnect. Restore, Release build, Compose, RabbitMQ topology and all 11 Python topology tests passed. [CI run #110](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44813451227).
- [ ] CI #111 for `8c975629dfb8dcba5f324c0bf90a7d532c0eb738` failed: 164/165 .NET tests passed (40/41 Challenge, 24 Habit, 100 Identity); the same commit-before-ack test timed out waiting for the consumer to reconnect after broker restart. Restore, Release build, Compose, RabbitMQ topology and all 11 Python topology tests passed. [CI run #111](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44822510001).
- [ ] CI #119 for `1b123541ea52dd845309977506eff170207fdcc2` failed: Release build succeeded with 0 warnings/errors; Docker Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed. .NET tests were 168/169 (45/46 Challenge, 24 Habit, 100 Identity), zero skipped. `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck` timed out after 30 seconds. Diagnostic evidence: connection attempts increased 1→137, established sessions remained 1→1, completed sessions reached 137, and the source queue held 1 message with 0 consumers. This proves repeated session setup is failing before a new consumer is established; the exact failing setup stage/type was not yet captured. [CI run #119](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44867775340).
- [x] README and communication documentation include Challenge local run/configuration guidance and transport contract.

## Deferred

- [ ] Implement Challenge programme/task/eligibility/participation APIs only under a separate approved SDD.
- [ ] Resolve inactive employee access to historical participation versus new mutations before employee-facing Challenge APIs (open question 19).
- [ ] Add SQS/EventBridge adapter and deploy AWS resources only after owner cloud setup.
- [ ] Do not claim global immediate JWT revocation.
- [ ] Do not implement inbox expiry/cleanup until retention/privacy/legal policy is approved.

- [ ] CI #120 for `0415c697e369ade72d19f6a5432850fd2e12f74e` failed: restore, Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 11 Python topology tests passed. .NET tests were 168/169 (Challenge 44/45, Habit 24/24, Identity 100/100). The sole failure was `Consumer_ReplaysCommittedMessageAsDuplicateWhenBrokerStopsBeforeAck`. Safe diagnostics showed 137 connection attempts, only one established session, 137 completed sessions, one source message and zero consumers; the last failure was at connection stage with `BrokerUnreachableException -> AggregateException -> ConnectFailureException -> SocketException`. [CI run #120](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44872585673).
