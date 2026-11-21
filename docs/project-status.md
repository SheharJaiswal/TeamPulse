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
