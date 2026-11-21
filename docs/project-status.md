## Insight consumer privacy regression and full-suite CI rerun verified

- Added a broker-backed privacy regression test using sentinel raw payload, optional note, credential and connection-string values; the test captures consumer logs and asserts none of those values are emitted.
- CI #193 attempt 1 passed the Insight tests (15/15) but failed during unrelated Habit and Challenge Testcontainers cleanup. CI #193 attempt 2 completed successfully for code commit `40b8e80d649af6b5d2eb8d4d14d6ec22b37601e0` (run attempt 2).
- The successful rerun passed restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all **19/19 Python RabbitMQ topology tests**, the pinned DynamoDB Local image pull, the full .NET suite and test-result artifact upload.
- .NET totals: Identity 100/100, Habit 24/24, Challenge 56/56, Event Tracking 27/27, Notification 28/28 and Insight 15/15 — **250/250 passed**, zero failed or skipped. The Challenge broker-loss replay/prefetch test remains green within its 56-test suite.
- Build succeeded with two nullable warnings confined to EventTracking and Notification test helpers; no Challenge build warning or error was reported.
- [CI #193, attempt 2](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289/artifacts/14703730990).
- This CI run validates code commit `40b8e80`; current branch HEAD at this update is a documentation-only descendant. No production code, persistent RabbitMQ queue/volume, MySQL volume or AWS resource was changed in this verification increment.

Updated: 2026-11-21

## Insight DynamoDB CI acceptance verified; RabbitMQ consumer tests added

- CI #188 passed for commit `faf0cac9ccc2c13f62fe4a9db25567959222d84f` on `feature/backend-foundation`.
- Release build, Docker Compose validation, local RabbitMQ startup/topology checks and the full .NET test suite passed. Test totals: Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28 and Insight 11 — **246/246 passed**, zero failed or skipped.
- The Insight DynamoDB Local integration tests verified minimal projection/inbox attributes, duplicate delivery, stale/equal/newer version behavior and concurrent version ordering.
- Added dedicated Insight RabbitMQ Testcontainers coverage for ack-after-processing, classified terminal DLQ delivery and source-message recovery when the DLQ route is unavailable. The acknowledgement test uses a prefetch-1 sentinel delivery to verify the first message was acknowledged before the next is processed.
- CI #190 passed for code commit `9ca613c41f9116296935270c19b8f8b136c78c5a`: **249/249 .NET tests passed**, zero failed or skipped (Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28, Insight 14). This includes the three new RabbitMQ Testcontainers tests for ack-after-processing, classified terminal DLQ delivery, and source-message recovery when the DLQ route is unavailable. Release build, Compose validation, RabbitMQ startup/topology checks, topology contract tests and test-result artifact upload also passed. [CI #190](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44893324977).
- No persistent RabbitMQ queue/volume, MySQL volume or AWS resource was modified.

Updated: 2026-11-21

## Insight lifecycle projection — implementation in progress

- Owner approval of the Insight lifecycle-projection SDD was received on 2026-11-21. Implementation is confined to `feature/backend-foundation`.
- Added Insight.Domain, Insight.Application, Insight.Infrastructure, Insight.API and Insight.UnitTests projects to the solution. The transport-independent schema-v1 processor validates Identity lifecycle envelopes, transitions, recognized fields and the optional note's string/1,000-character bound; the note is excluded from the persistence contract.
- Added a DynamoDB-native store using strongly consistent reads and `TransactWriteItems` for the inbox/projection decision, conditional version fences and bounded optimistic-concurrency retries. The store records only approved projection/inbox attributes.
- Added Development-only opt-in DynamoDB Local table initialization, DynamoDB readiness health, independent consumer health and a disabled-by-default RabbitMQ consumer with manual acknowledgement, bounded transient retry/reconnect and confirmed DLQ publication.
- Added DynamoDB Local to Compose without changing or resetting existing MySQL/RabbitMQ volumes. Insight source/DLQ topology remains unchanged: the committed definitions retain the existing durable classic source queue and original binding. Target quorum/DLQ topology is documented only; activation awaits a reviewed migration utility and controlled maintenance window.
- The processor foundation and consumer/store code are committed; CI restore/build/test validation is in progress or pending. Do not treat the Insight increment as complete until DynamoDB Local, RabbitMQ integration, topology and full CI evidence pass.
- No AWS resources, EventBridge/SQS adapters, analytics, dashboards or read APIs were added. The migration procedure is documented at `docs/changes/insight-employee-status-projection-consumer/rabbitmq-migration-runbook.md`; it is not authorized for execution against a live persistent broker.

Updated: 2026-11-21

## Notification employee-status projection — acceptance verified by CI #168

- CI #168 passed for commit `bd27c4a118e851d02625d5e2002a9be357acec0a` on `feature/backend-foundation`.
- Test results: Notification **28/28**, EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **235/235 .NET tests passed**, zero failed or skipped; RabbitMQ topology contract **17/17** passed.
- Release build succeeded with 0 errors. Docker Compose validation, RabbitMQ startup/topology checks and test-result artifact upload passed.
- Notification's malformed UTF-8 test verified original message bytes and `InvalidEvent` classification reached its DLQ. The unavailable-DLQ-route test verified that the source delivery remained unacknowledged and was recoverable after the consumer session ended. Both are isolated RabbitMQ Testcontainers tests.
- The Notification employee-status projection SDD acceptance criteria NESP-01–NESP-14 are now verified by the combined implementation tests and CI #168. Scope remains projection-only; this does not authorize notification sending/preferences changes, employee-facing APIs, or an inactive-employee access policy.
- [CI #168](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669). Test results artifact: [teampulse-test-results](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669/artifacts/14700589953).
- Build reported two nullable warnings in integration-test helper code (one Notification, one EventTracking); there were no build errors. These are recorded for a later cleanup and do not invalidate the passing run.
- No persistent RabbitMQ broker, existing database volume, or AWS resource was changed.

Updated: 2026-11-21

## Notification DLQ failure-path test added

- Added a second broker-backed failure-path test: temporarily remove the Notification DLQ binding, publish malformed UTF-8 to the source queue, and verify the mandatory DLQ publish failure ends the consumer session without acknowledging the source delivery. After channel disposal, the test reads and acknowledges the redelivered source message to leave its disposable broker clean.
- Together with the malformed-message DLQ success-path test, this covers both confirmed DLQ publication and unavailable DLQ routing. CI verification is pending.
- These tests use isolated RabbitMQ Testcontainers instances; they do not alter the persistent local broker or production topology.

Updated: 2026-11-21

## Notification consumer CI verification and DLQ test extension

- CI #165 passed for commit `13095cc3effb30a11862ccc68a64fc3ebf60425e` on `feature/backend-foundation`: restore, Release build, Docker Compose validation, RabbitMQ startup/topology contract tests and the full .NET suite (including MySQL 8.4 and RabbitMQ Testcontainers) all succeeded.
- This verifies the Notification API host, persistence, stale/equal lifecycle-version classification and existing broker replay integration coverage.
- Added a focused Testcontainers case for malformed UTF-8 delivery: assert original bytes and `InvalidEvent` classification are published to the Notification DLQ, then source queue empties after confirmed publication. Verification is pending in the next CI run.
- Remaining Notification acceptance work: verify DLQ routing failure leaves the source delivery unacknowledged/recoverable, then update acceptance evidence. No persistent/shared RabbitMQ broker or database volume was changed.

Updated: 2026-11-21

## Notification lifecycle consumer and broker topology added

- Added the opt-in Notification RabbitMQ consumer with manual ack after MySQL commit, bounded transient retry/backoff, reconnect, dedicated publisher-confirmed DLQ publishing and independent `/health/consumer`.
- Added Notification quorum source/DLQ topology with delivery limit 5 and at-least-once dead-lettering. Updated only Notification topology; all other service queues retain their prior definitions.
- Added Notification RabbitMQ/MySQL replay integration coverage, consumer options/retry tests, API-host consumer health coverage and Python topology contract assertions.
- Added `docs/runbooks/notification-rabbitmq-queue-migration.md`. It requires a maintenance window, stopping publisher/consumer, buffering pending messages, recreating only Notification's immutable queue, restoring messages, and verifying before resuming. No live RabbitMQ queue or shared volume was modified.
- Implementation commit containing consumer and topology: `b580ca14308d25cd72f63083a9839c62ef509998` and `22598c2d58e9befd537c29578c62f5e6f10017f6`; replay tests and checklist are included in the current execution.
- CI verification pending. The last confirmed Notification API build was CI #158 before consumer addition; CI #161 is running against the consumer/topology increment.

Updated: 2026-11-21

## Notification projection — MySQL persistence increment added

- Added Notification.Infrastructure with EF Core 8/Pomelo, a checked-in initial migration/model snapshot, a transactional inbox + conditional lifecycle-version upsert store, and a read-only projection reader abstraction.
- Added MySQL 8.4 Testcontainers coverage for exact schema columns, migration idempotency, duplicate event IDs, stale/equal versions and concurrent delivery preserving the maximum lifecycle version.
- Added `04-notification-database.sql`, mounted it in Compose, and documented safe manual database/grant creation for existing persistent MySQL volumes. No volume reset or live database mutation was performed.
- Commit: `7f92cf4128f9ca3fd2f72ca4d8a8f4ac88654732`. CI verification pending. Next increment is Notification.API host/startup migration and separate health endpoints.

Updated: 2026-11-21

## Notification projection implementation — application core added

- Owner approval for the Notification employee-status projection SDD was received on 2026-11-21. The proposal/spec/design/tasks are now marked owner-approved; the scope remains projection-only.
- Added Notification.Domain, Notification.Application and Notification.UnitTests projects to the six-service solution. The first increment implements minimal projection/inbox domain entities, strict schema-v1 recognized-field validation and a transport-independent processor.
- Added deterministic tests for valid lifecycle transitions, exact `hr-admin-api` source, reason-code bounds, malformed/missing fields, unsupported schema, transition mismatch, unknown additive fields and optional-note exclusion.
- Implementation commit: `d8e207f7a7650bc1747baf8509e17c5f4e2b305f`. CI verification for this commit is pending; no local .NET or Docker build/test is claimed.
- Next focused increment: Notification-owned EF Core/MySQL persistence and checked-in migration. RabbitMQ source-queue migration remains a later increment and must preserve pending messages; no live broker or existing database volume has been altered.

Updated: 2026-11-21

## Next increment prepared — Notification lifecycle projection SDD draft

- Prepared `docs/changes/notification-employee-status-projection-consumer/` with proposal, spec, design and acceptance-mapped tasks on 2026-11-21.
- Scope is limited to the Notification-owned employee-status projection/inbox and opt-in local RabbitMQ consumer. No notification sending, preference changes, delivery-history mutations, AWS provisioning or retention cleanup are included.
- The current local Notification queue is durable but has no quorum/DLX arguments. The draft requires an explicit pending-message-preserving migration before changing its immutable queue type/arguments; no live broker was modified.
- The SDD is **awaiting owner approval**. No Notification application code, database migration, Compose change or RabbitMQ topology change has been implemented. Implementation remains blocked until approval.

Updated: 2026-11-21

## Event Tracking lifecycle projection consumer acceptance — CI #151 verified

- Live implementation head: `d9402b56fd91ad59a1de34a8aee6fcfcde905db0` on `feature/backend-foundation`; current branch documentation is updated after that tested code commit.
- The owner-approved Event Tracking projection-consumer SDD acceptance criteria ETESP-01–ETESP-14 are complete. The API-host test verifies the real opt-in RabbitMQ consumer becomes healthy, then reports consumer HTTP 503 after broker shutdown while MySQL readiness remains HTTP 200.
- The broker interruption/replay integration test uses the production Event Tracking processor and MySQL inbox. It commits the first delivery, interrupts RabbitMQ before acknowledgement, restarts the same broker container, and verifies redelivery is classified Duplicate without a second projection mutation.
- CI #151 passed: EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **207/207 .NET tests**; **14/14 Python RabbitMQ topology tests**; Docker Compose validation and RabbitMQ startup/topology checks passed. Release build had 0 errors and one nullable warning in `EventTrackingRabbitMqMySqlReplayIntegrationTests.cs`.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- This closes only the Event Tracking employee-status projection foundation. Activity submission/evidence APIs and post-deactivation access policy, Insight/Notification consumers, AWS EventBridge/SQS provisioning, inbox retention/legal sign-off and global token revocation remain deferred. No AWS resources were provisioned.

Updated: 2026-11-21

## Challenge lifecycle projection acceptance — CI #151 verified

- Live code head verified: d9402b56fd91ad59a1de34a8aee6fcfcde905db0 on feature/backend-foundation. The deterministic Challenge fingerprint-vector test is present in this head's history.
- CI #151 completed successfully for that exact head: Release build succeeded with 0 errors and one nullable warning in EventTrackingRabbitMqMySqlReplayIntegrationTests.cs (outside Challenge scope); Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed.
- All .NET tests passed: **207/207** — Challenge 56, EventTracking 27, Habit 24 and Identity 100; zero failed or skipped. The Challenge known-answer test verified digest f17c59bcb345810da58183dc56a79b7eaa6e742ee8e9f5fb4432993eb8909fc5 across property-order/whitespace equivalence, UUID case normalization, equivalent UTC timestamps and long.MaxValue.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- The approved Challenge projection acceptance criteria CEPS-01–CEPS-18 are now verified by the CI evidence in the Challenge SDD tasks. This closes projection implementation acceptance only; it does not authorize Challenge business workflows. Open question 19, privacy/legal retention sign-off and owner-deferred AWS provisioning remain unchanged.

Updated: 2026-11-21

## Previous repository checkpoint before CI #151 — 2026-11-21

- Active branch: `feature/backend-foundation`. Current head at this documentation update: `ee99cdb24994a5a06c9efcdc5eeb8c624ff1b2a2` (an EventTracking-only follow-up; no Challenge files changed).
- CI #145 ran against Challenge commit `5fb1d41f6a775af88968d3277414fe77627edc10`. Restore, Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed. Challenge passed 55/55, Habit 24/24 and Identity 100/100. EventTracking passed 25/26; its sole failure was the API-host consumer-health test waiting for `/health/consumer` to return 503 after RabbitMQ shutdown. Thus 204/205 .NET tests passed and all 55 Challenge tests passed. [CI #145](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44882807000).
- The concurrent equal-lifecycle-version outcome race is closed: the MySQL 8.4 Testcontainers regression verifies exactly one Applied and one Stale, with the committed projection matching the winning event. The connection-scoped result flag is set by the atomic duplicate-key conditional upsert; CI #145 verified the full Challenge test project.
- CI #146 is running for the EventTracking-only follow-up commit `ee99cdb`; it is not a Challenge acceptance change. The current full-workflow failure remains outside the approved Challenge increment.
- Challenge scope remains projection-only. Open question 19, permanent inbox retention pending privacy/legal review and AWS EventBridge/SQS/CloudWatch provisioning remain deferred.


## Event Tracking RabbitMQ Testcontainers coverage

- Added broker-backed tests using RabbitMQ 4.1 Testcontainers for manual acknowledgement after processing completes, terminal outcome DLQ confirmation/classification, transient retry success and exhaustion, source redelivery when the mandatory DLQ route is unavailable, and log privacy.
- These tests use a dedicated disposable broker and the exact Event Tracking source/DLQ quorum topology. They do not modify the user's existing persistent broker volume.
- CI #137 completed successfully: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — 195 .NET tests passed, plus all 14 Python topology tests. The API-host startup/MySQL integration test passed.
- The Event Tracking RabbitMQ broker tests were included in CI #142/#143. Both runs passed the new broker/consumer cases but failed the API-host consumer-health isolation test: 25/26 EventTracking tests passed; the failure waited for `/health/consumer` to return 503 after broker shutdown. This Event Tracking issue is outside the Challenge SDD scope.

Updated: 2026-11-21


## Event Tracking API-host startup CI verification

- CI #137 passed for commit `454e9496d8d53c6ea89c425e535704400a11c397`: Release build, Compose validation, RabbitMQ startup/topology and all automated tests succeeded. [CI #137](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44880083557).
- Test results: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — **195 .NET tests passed**; all 14 Python RabbitMQ topology tests passed.
- EventTracking's MySQL 8.4 Testcontainers API-host startup test passed, proving the production API host applies its migration and resolves the production projection store/reader.
- Added RabbitMQ Testcontainers consumer tests for acknowledgement ordering, terminal DLQ classification/confirmation, transient retry success and exhaustion, DLQ route failure/redelivery, and logging privacy. CI verification for these newly added tests is pending.

Updated: 2026-11-21


## RabbitMQ integration-test compile correction

- CI #138 caught four compile errors in the new broker tests: xUnit selected its DateTime overload for `Assert.Equal(0, uintQueueCount)`. The queue-count assertions now use an explicit unsigned expected value (`0u`).
- This is a test-only compile correction. The test suite did not execute in CI #138; do not treat the new RabbitMQ integration tests as verified until the next CI run succeeds.

Updated: 2026-11-21
