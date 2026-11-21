## Latest repository checkpoint — 2026-11-21

- Active branch: `feature/backend-foundation`. Current head at this checkpoint: `35fbaabe8e70ad271f3d6e2e6245f2d0294e927d` (`fix(challenge): classify concurrent equal-version projections`).
- CI #144 for that commit failed after restore, Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed. .NET results: Challenge 52/55, Habit 24/24, Identity 100/100, EventTracking 25/26. The three Challenge failures were Stale outcome assertions for sequential lower/equal versions and the new concurrent equal-version race test; the attempted affected-row result was positive for no-op upserts. The separate EventTracking consumer-health test also failed waiting for `/health/consumer` to return 503 after broker shutdown. [CI #144](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44882416187).
- Corrective follow-up replaces affected-row classification with a connection-scoped MySQL flag set by the duplicate-key upsert itself. It distinguishes insert/newer-version application from equal/lower-version no-op without a pre-read or schema change. The new regression test remains pending CI verification.
- The EventTracking failure is outside this approved Challenge increment and is not being modified here. The Challenge implementation remains projection-only; open question 19, permanent inbox retention pending privacy/legal review and AWS EventBridge/SQS/CloudWatch provisioning remain deferred.


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
