


## Event Tracking RabbitMQ Testcontainers coverage

- Added broker-backed tests using RabbitMQ 4.1 Testcontainers for manual acknowledgement after processing completes, terminal outcome DLQ confirmation/classification, transient retry success and exhaustion, source redelivery when the mandatory DLQ route is unavailable, and log privacy.
- These tests use a dedicated disposable broker and the exact Event Tracking source/DLQ quorum topology. They do not modify the user's existing persistent broker volume.
- CI #137 is still running the API-host startup/MySQL integration test stage; latest test results must be polled before claiming the startup increment passed.
- Commit for the new broker tests is pending CI verification. Commit-before-ack replay against the real persistent inbox and API-host consumer health isolation remain outstanding.

Updated: 2026-11-21


## Event Tracking API-host startup CI verification

- CI #137 passed for commit `454e9496d8d53c6ea89c425e535704400a11c397`: Release build, Compose validation, RabbitMQ startup/topology and all automated tests succeeded. [CI #137](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44880083557).
- Test results: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — **195 .NET tests passed**; all 14 Python RabbitMQ topology tests passed.
- EventTracking's MySQL 8.4 Testcontainers API-host startup test passed, proving the production API host applies its migration and resolves the production projection store/reader.
- Added RabbitMQ Testcontainers consumer tests for acknowledgement ordering, terminal DLQ classification/confirmation, transient retry success and exhaustion, DLQ route failure/redelivery, and logging privacy. CI verification for these newly added tests is pending.

Updated: 2026-11-21
