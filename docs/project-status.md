## Latest repository checkpoint — 2026-11-21

- Active branch: `feature/backend-foundation`. Branch head before the Challenge concurrency correction: `9901c0f2e9487e0e85e2ded1458c622983090a54`.
- Latest CI #143 failed overall after a clean Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests. The .NET suite had 203 passed and 1 failed: Challenge 54/54, Habit 24/24, Identity 100/100, EventTracking 25/26. The sole failure was EventTracking API-host consumer health remaining non-503 after RabbitMQ shutdown. [CI #143](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44881977542). CI #142 showed the same failure; the heartbeat change in #143 did not resolve it. No EventTracking changes are included in the Challenge increment.
- Challenge's approved projection acceptance remains otherwise verified: broker commit-before-ack replay/reconnect in CI #122, MySQL concurrency/migration tests in CI #128, health/schema/privacy/DLQ coverage in CI #127/#129, and real API-host startup/migration/production-reader integration in CI #133. The current CI #143 run also passed all 54 Challenge tests.
- A code review found a narrow outcome-classification race for concurrent distinct event IDs carrying the same lifecycle version. The conditional upsert prevents a second projection mutation, but the previous pre-read/final-version heuristic could report both deliveries as Applied. The current increment changes classification to use the atomic upsert's affected-row result with MySqlConnector `UseAffectedRows=true`, and adds a deterministic MySQL 8.4 regression test. CI verification is pending.
- Challenge scope remains projection-only. Open question 19, permanent inbox retention pending privacy/legal review, and AWS EventBridge/SQS/CloudWatch provisioning remain deferred.




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
