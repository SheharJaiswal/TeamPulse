


## Event Tracking foundation — first CI failure diagnosed and startup fix prepared

- Event Tracking SDD was explicitly approved on 2026-11-21. The initial foundation commit is `1b2f45b49b16d03d2b2bd7ca777528009f7fb1fa`.
- CI #134 (run `44878632435`) restored successfully but Release build failed in `EventTracking.API/Program.cs`: the `GetConnectionString` extension was unavailable because `Microsoft.Extensions.Configuration` was not imported. Tests and later workflow stages were skipped; no test pass is claimed.
- Corrected the missing namespace import. Restore also reported NU1902 for explicitly pinned MySqlConnector 2.3.7; the Event Tracking infrastructure reference is upgraded to 2.4.0 to address that reported dependency advisory.
- Added `docs/runbooks/event-tracking-local-migrations.md` with the non-destructive database/grant command for existing persistent MySQL volumes, new-volume bootstrap behavior, startup EF migration behavior and the explicit RabbitMQ migration boundary.
- The Event Tracking tasks now distinguish completed database/runbook work from pending CI verification, API-host startup integration, and RabbitMQ consumer/topology work.
- No RabbitMQ queue or shared volume was modified. No local .NET/Docker execution is claimed. Awaiting CI for this correction before proceeding to consumer implementation.

Updated: 2026-11-21


## Event Tracking consumer increment — implementation committed, CI pending

- CI #135 passed for foundation commit `f794d545d14dec038ca80bb2f6b59bfee7d98a3e`: Release build, Docker Compose validation, RabbitMQ startup/topology and MySQL 8.4 Testcontainers tests all passed. [CI #135](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44879380833).
- Added EventTracking's opt-in RabbitMQ hosted consumer, manual acknowledgement after committed projection processing, prefetch 1, bounded full-jitter transient retry, confirmed mandatory DLQ publication before source ack, session teardown/reconnect and independent consumer health.
- Added Event Tracking's quorum source queue arguments and dedicated quorum DLQ, plus exact Python topology contract tests and initial retry/options unit tests.
- Added `docs/runbooks/event-tracking-rabbitmq-queue-migration.md`. Existing persistent broker volumes remain untouched; the runbook requires pausing publisher/consumer, moving pending messages to a durable migration buffer, verifying counts, replacing only the Event Tracking queue and restoring the messages before resuming traffic.
- This consumer/topology increment is not yet CI-verified. API-host startup migration and full RabbitMQ Testcontainers acknowledgement/retry/DLQ/reconnect/privacy tests remain pending. No local .NET/Docker execution is claimed.

Updated: 2026-11-21
