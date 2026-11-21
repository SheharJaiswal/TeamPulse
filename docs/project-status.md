


## Event Tracking RabbitMQ Testcontainers coverage

- Added broker-backed tests using RabbitMQ 4.1 Testcontainers for manual acknowledgement after processing completes, terminal outcome DLQ confirmation/classification, transient retry success and exhaustion, source redelivery when the mandatory DLQ route is unavailable, and log privacy.
- These tests use a dedicated disposable broker and the exact Event Tracking source/DLQ quorum topology. They do not modify the user's existing persistent broker volume.
- CI #137 is still running the API-host startup/MySQL integration test stage; latest test results must be polled before claiming the startup increment passed.
- Commit for the new broker tests is pending CI verification. Commit-before-ack replay against the real persistent inbox and API-host consumer health isolation remain outstanding.

Updated: 2026-11-21
