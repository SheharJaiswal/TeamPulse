


## Event Tracking foundation — first CI failure diagnosed and startup fix prepared

- Event Tracking SDD was explicitly approved on 2026-11-21. The initial foundation commit is `1b2f45b49b16d03d2b2bd7ca777528009f7fb1fa`.
- CI #134 (run `44878632435`) restored successfully but Release build failed in `EventTracking.API/Program.cs`: the `GetConnectionString` extension was unavailable because `Microsoft.Extensions.Configuration` was not imported. Tests and later workflow stages were skipped; no test pass is claimed.
- Corrected the missing namespace import. Restore also reported NU1902 for explicitly pinned MySqlConnector 2.3.7; the Event Tracking infrastructure reference is upgraded to 2.4.0 to address that reported dependency advisory.
- Added `docs/runbooks/event-tracking-local-migrations.md` with the non-destructive database/grant command for existing persistent MySQL volumes, new-volume bootstrap behavior, startup EF migration behavior and the explicit RabbitMQ migration boundary.
- The Event Tracking tasks now distinguish completed database/runbook work from pending CI verification, API-host startup integration, and RabbitMQ consumer/topology work.
- No RabbitMQ queue or shared volume was modified. No local .NET/Docker execution is claimed. Awaiting CI for this correction before proceeding to consumer implementation.

Updated: 2026-11-21
