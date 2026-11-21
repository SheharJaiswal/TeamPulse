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
- [x] Verify inbox has no TTL/automatic cleanup and stores no raw payload or note: CI #127 passed the MySQL schema assertion for exactly the eight approved metadata/fingerprint columns and no scheduled cleanup events.
- [x] Align health response states with separate liveness, MySQL readiness and RabbitMQ consumer-health contract: CI #127 passed the production health-check integration test with healthy MySQL and disconnected RabbitMQ.


- [x] Diagnose CI #120 reconnect failure: the last failure stage was `connection` with `BrokerUnreachableException -> AggregateException -> ConnectFailureException -> SocketException`; the source queue retained one message and had zero consumers. The Challenge Testcontainers fixture used a dynamically mapped host port, while the consumer retained its original URI across container restart.
- [x] Stabilize the Challenge Testcontainers AMQP host port across stop/start, capture the connection string after initial startup and verify it remains unchanged after restart; CI #122 passed the complete broker-loss replay and sentinel/prefetch acceptance test.

## Verification gaps

- [ ] Dedicated API-host startup integration test authored in `ChallengeApiStartupIntegrationTests.cs`: starts the real `Program` against the fixture's fresh MySQL 8.4 database with the RabbitMQ consumer disabled, calls `/health/ready`, asserts both checked-in migrations were applied, writes through the production projection store and reads through its production reader. Awaiting CI runtime verification. Lower-level repeated migration-history and fresh-database health/readiness checks passed CI #128/#127.
- [x] MySQL 8.4 Testcontainers: sequential same event ID/same fingerprint and same event ID/different fingerprint are covered. Concurrent same-ID/same-fingerprint and conflicting-fingerprint tests have now been added; awaiting CI verification.
- [x] MySQL 8.4 Testcontainers: duplicate-key race safely reads the committed fingerprint after rolling back the losing insert; concurrent same-fingerprint and conflicting-fingerprint tests passed CI #128.
- [x] MySQL 8.4 Testcontainers: concurrent N/N+1 processing leaves maximum lifecycle version; rollback leaves neither inbox nor projection mutation (existing integration tests passed CI #127).
- [x] RabbitMQ Testcontainers: duplicate redelivery after commit-before-ack and idempotent replay; verified by CI #122 after stable endpoint correction (47 Challenge tests passed).
- [x] RabbitMQ Testcontainers: invalid event and integrity-violation classifications, mandatory DLQ confirmation, and unacknowledged source delivery when DLQ publication cannot be confirmed; verified in CI #107/#108/#127.
- [x] RabbitMQ Testcontainers: bounded retry then success, retry exhaustion, non-transient no-retry classification, and unacknowledged source delivery when retry-exhaustion DLQ publication cannot be confirmed; verified in CI #108/#129.
- [x] RabbitMQ Testcontainers: broker unavailable at startup and reconnect; startup-reconnect test passed in CI #105.
- [x] RabbitMQ Testcontainers: established-session loss/reconnect and duplicate redelivery after commit-before-ack; CI #122 passed the complete test, including cancellation of the active callback, old-session disposal, replay as Duplicate and sentinel processing proving prefetch-1 acknowledgement release. CEPS-11/CEPS-14 acceptance is verified.
- [x] CI #125 diagnosis: Release build (0 warnings/errors), Compose validation and all 11 Python topology tests passed; .NET results were Challenge 48/49, Habit 24/24 and Identity 100/100. The sole failure was `Consumer_LeavesOriginalUnacknowledgedWhenDeadLetterPublishCannotBeConfirmed`: the test inspected the source queue after `IsConnected` became false but before the session's channels completed disposal and RabbitMQ requeued the unacknowledged message. Production session teardown already exposes `CompletedSessionCount`; the test now captures the baseline before publishing, then waits for the count to advance before reading the queue. [CI #125](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44874441259).
- [x] Add monotonic in-memory connection-attempt and established-session counters and include them, plus source queue message/consumer counts, in bounded replay-test timeout diagnostics. These counters do not change the public health contract; established-session replay passed in CI #122.
- [x] Add in-memory session-setup stage and last-failure exception-type-chain diagnostics so reconnect failures can be localized to connection, channel creation, QoS or consumer registration. Store no exception messages, broker URI or credentials; verify this with a unit test.
- [x] Bound RabbitMQ connection-attempt, AMQP handshake and protocol continuation timeouts through a validated consumer option; deterministic tests reject out-of-range values. Established-session reconnect verified in CI #122.
- [x] Add a completed-session counter that advances after RabbitMQ channels/connection finish disposal; the established-session replay test waits for this signal before restarting the broker. Established-session disposal and replay were verified end-to-end in CI #122.
- [x] RabbitMQ Testcontainers: restore the Challenge DLQ binding after a mandatory publish failure and verify the original unacknowledged delivery is processed and dead-lettered after reconnect; verified by `Consumer_ReconnectsAndDeadLettersRedeliveryAfterDeadLetterRouteIsRestored` in CI #129.
- [x] Host health integration tests prove DB readiness is independent of RabbitMQ and consumer health is separately observable; CI #127.
- [x] MySQL schema and consumer log privacy tests prove no raw event body/note is persisted or emitted, no broker credentials/URI are logged, and inbox retention has no automatic cleanup; CI #127/#129.
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

- [x] CI #122 passed for `725b4a887eeccd83709f9c36dfc33ccf6d776db8`: Release restore/build, Docker Compose validation, RabbitMQ startup/topology and all 11 Python tests passed; .NET tests passed 171/171 (Challenge 47, Habit 24, Identity 100), zero failed/skipped. The established-session broker-loss replay and sentinel/prefetch acceptance test passed. [CI run #122](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44873516708).
- [x] MySQL inbox schema/privacy assertion: exactly eight approved metadata/fingerprint columns and no scheduled cleanup events; CI #127.

- [x] Health-check integration coverage proves liveness, MySQL readiness and RabbitMQ consumer health are independently selectable; healthy MySQL remains ready while RabbitMQ is disconnected; CI #127.

- [x] CI #127 passed for `50e844e0f01290e393187801d1391d9ec189b878`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology, 11 Python topology tests and all 173 .NET tests (Challenge 49, Habit 24, Identity 100), 0 failed/skipped. This verifies the completed-session synchronization correction and the inbox schema/privacy and health-check integration tests. [CI run #127](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44874735877).
- [x] CI #128 passed for `0833da57289080ed8cdabe8c97fe98d1f12a7da5`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology, 11 Python topology tests and all 176 .NET tests (Challenge 52, Habit 24, Identity 100), 0 failed/skipped. Verified concurrent same-ID/same-fingerprint handling, concurrent conflicting-fingerprint winner preservation, and repeated migration application/history stability. [CI run #128](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44875036205).
- [x] Capturing-logger RabbitMQ integration test proves event bodies, optional notes, broker credentials and broker URI are not emitted by the Challenge consumer; passed CI #129.


## Latest verification checkpoint — Challenge consumer privacy and DLQ recovery

- Active branch: `feature/backend-foundation`. Code commit under test: `a73992143617fb8cd36093a45b32752b14f78863`.
- CI #129 passed: [GitHub Actions run #129](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44875363047). Restore, Release build, Docker Compose validation, RabbitMQ startup/topology and 11 Python topology tests passed. The .NET suite passed 177/177: Challenge 53, Habit 24, Identity 100; zero failed/skipped. The new Challenge capturing-logger test passed.
- Verified DLQ recovery: after a mandatory dead-letter publish fails because the DLQ binding is absent, the original source delivery remains unacknowledged; after the binding is restored and the consumer reconnects, the redelivery is successfully dead-lettered with the expected classification.
- Existing RabbitMQ coverage for terminal validation classifications, transient retry success, retry exhaustion, non-transient failures and failed DLQ confirmation is reconciled as verified against CI #107/#108/#129.
- Concurrent same-ID/same-fingerprint and conflicting-fingerprint MySQL race tests plus repeated migration-history stability passed CI #128 (176/176 .NET tests). Schema/privacy and health-check integration passed CI #127.
- Remaining acceptance gap: add an explicit production API-host integration test that starts against a clean MySQL database, verifies the host startup migration path, and reads a projection through the production reader. Lower-level fresh-database migration/readiness and repeated migration-history tests have passed, but do not claim this full host-startup path is verified.
- Local .NET SDK and Docker CLI remain unavailable in this environment; all build/test/container evidence above is from GitHub Actions.
- Deferred: AWS EventBridge/SQS/CloudWatch until owner cloud setup; inbox retention remains permanent until privacy/legal review; open question 19 remains unresolved and blocks employee-facing Challenge business APIs.
- Next increment: close the explicit clean API-host startup/projection-reader test gap, without expanding Challenge product scope.


## API-host startup migration and projection-reader test — CI pending

- Added `ChallengeApiStartupIntegrationTests` using `WebApplicationFactory<Program>` and a fresh MySQL 8.4 Testcontainers fixture.
- The test starts the actual Challenge API host with its RabbitMQ consumer disabled, calls the production readiness endpoint, asserts both checked-in EF migrations are applied, then writes and reads an employee projection through the registered production store.
- Added `Microsoft.AspNetCore.Mvc.Testing` 8.0.11 to the Challenge test project.
- Keep this acceptance item unchecked until GitHub Actions confirms package restore, host startup, migration execution and projection read.


## CI #130 failure analysis and test-host configuration correction

- CI #130 (`8dca20f026034f1fff18939e3c8441754b392337`) passed restore, Release build, Compose, RabbitMQ topology and Python tests. .NET result: 230 passed / 1 failed (Challenge 53/54, Habit 24/24, Identity 100/100).
- The only failure was the new API-host startup test. Stack trace showed `ServerVersion.AutoDetect` attempted to connect using the appsettings localhost connection string, not the Testcontainers connection string. The existing Habit API integration factory establishes test settings with both `UseSetting` and `ConfigureAppConfiguration`, and sets Testing environment.
- Applied the same proven test-host configuration pattern in a dedicated `ChallengeApiFactory`; disabled the consumer in the in-memory test configuration. The test logic and production API startup path remain unchanged.
- Re-run pending. Keep API-host acceptance unchecked until CI verifies host startup, migrations and projection read.
